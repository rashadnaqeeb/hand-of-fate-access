#if HOF_FMOD
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// IAudioBackend over an FMOD Core <c>System</c>, the dedicated-engine path that
	/// replaces Unity's AudioSource pool. FMOD is chosen for three things Unity's mixer
	/// denies us: a flat stereo pan law (<c>Channel.setPan(-1..1)</c> places a cue fully
	/// in one ear with no constant-power curve), much lower trigger latency (a small DSP
	/// buffer set before init, versus Unity's ~200ms), and no positional postprocessing
	/// the engine applies behind our back. The spatial grammar Core computes is the one
	/// the player hears.
	///
	/// Compiled only when the build defines HOF_FMOD (set HofFmod=true), since it needs
	/// the license-gated FMOD SDK: the official <c>fmod.cs</c> binding (compiled in by the
	/// csproj) and the native x86 <c>fmod.dll</c> (preloaded from the plugins folder by
	/// NativeLoader before the first call, exactly as Tolk and the SAPI shim are). Without
	/// the define the repo builds against the Unity backend alone.
	///
	/// One FMOD System runs alongside the System the game's own engine (Unity 5.3 is FMOD
	/// under the hood) already owns. The default output is the shared OS mixer, which does
	/// not take the device exclusively, so the two coexist; init logs the driver and output
	/// so a device conflict is visible rather than guessed. Slot bookkeeping is the shared
	/// engine-agnostic <see cref="VoicePool"/>; this class binds slots to FMOD Channels and
	/// reclaims slots whose one-shot has finished.
	/// </summary>
	public sealed class FmodAudioBackend : IAudioBackend {
		// Matches the Unity backend's pool: the wall tones plus transient one-shots. FMOD's
		// own software channel limit is set well above this at init.
		private const int VoiceCount = 24;

		// A small DSP buffer trades a little dropout headroom for low output latency, the
		// whole reason for leaving Unity. 512 frames x 4 buffers is a conservative low
		// value (~40ms of mix-ahead, one buffer of which is the added trigger latency);
		// must be set before System.init. Tune down further if cues still feel late.
		private const uint DspBufferLength = 512;
		private const int DspBufferCount = 4;

		private FMOD.System _system;
		private FMOD.ChannelGroup _master;
		private readonly FMOD.Channel[] _channels = new FMOD.Channel[VoiceCount];
		private readonly Dictionary<string, FMOD.Sound> _sounds = new Dictionary<string, FMOD.Sound>();
		private VoicePool _pool;
		private bool _initialized;
		private bool _available;

		public bool IsInitialized => _initialized;
		public bool IsAvailable => _available;

		public bool Initialize() {
			if (_initialized) return _available;
			_initialized = true;

			try {
				FMOD.RESULT r = FMOD.Factory.System_Create(out _system);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod System_Create failed: {r}");
					_available = false;
					return false;
				}

				// Lower the output latency before init; non-fatal if the driver refuses it.
				r = _system.setDSPBufferSize(DspBufferLength, DspBufferCount);
				if (r != FMOD.RESULT.OK)
					Log.Warn($"fmod setDSPBufferSize failed: {r}; using the driver default");

				r = _system.init(VoiceCount * 2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod System.init failed: {r}");
					_system.release();
					_available = false;
					return false;
				}

				_system.getMasterChannelGroup(out _master);
				_pool = new VoicePool(VoiceCount);
				_available = true;

				LogDeviceInfo();
				Log.Info($"fmod audio backend initialized with {VoiceCount} voices");
				return true;
			} catch (Exception ex) {
				// A DllNotFoundException here means fmod.dll was not preloaded or is missing.
				Log.Error(ex is DllNotFoundException
					? $"fmod.dll not found (preload it from the plugins folder): {ex}"
					: $"fmod audio backend init failed: {ex}");
				_available = false;
				return false;
			}
		}

		// Surface the running output and version so a clash with the game's own FMOD System
		// (or a silent fallback to the NOSOUND output) is visible in the log, not a mystery.
		// Kept to calls whose binding signatures are stable across the FMOD 2.0x line.
		private void LogDeviceInfo() {
			_system.getVersion(out uint version);
			_system.getOutput(out FMOD.OUTPUTTYPE output);
			_system.getDSPBufferSize(out uint blockLen, out int blockCount);
			Log.Info($"fmod {version >> 16}.{(version >> 8) & 0xFF:D2}.{version & 0xFF:D2}, " +
				$"output {output}, dsp buffer {blockLen}x{blockCount}");
		}

		public void Pump() {
			if (!_available) return;
			// FMOD requires one System.update per frame for voice management and housekeeping.
			_system.update();
		}

		public void Shutdown() {
			if (!_initialized) return;

			try {
				if (_available) {
					StopAll();
					foreach (FMOD.Sound sound in _sounds.Values)
						sound.release();
					_sounds.Clear();
					_system.release();
					Log.Info("fmod audio backend shutdown");
				}
			} catch (Exception ex) {
				Log.Warn($"fmod audio backend shutdown error: {ex}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		public void Register(string key, float[] pcm, int channels, int sampleRate) {
			if (!_available) return;

			try {
				if (pcm.Length % channels != 0)
					Log.Warn($"fmod register '{key}': PCM length {pcm.Length} is not a multiple of {channels} channels; trailing samples dropped");

				// Hand FMOD the raw float PCM straight from Core's synths: OPENMEMORY reads the
				// supplied buffer, OPENRAW says there is no header so the format comes from the
				// exinfo, CREATESAMPLE loads it fully into memory (short, often-looped cues), and
				// _2D disables 3D rolloff so only our pan/volume position the sound.
				byte[] bytes = new byte[pcm.Length * sizeof(float)];
				Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);

				FMOD.CREATESOUNDEXINFO ex = new FMOD.CREATESOUNDEXINFO();
				ex.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
				ex.length = (uint)bytes.Length;
				ex.numchannels = channels;
				ex.defaultfrequency = sampleRate;
				ex.format = FMOD.SOUND_FORMAT.PCMFLOAT;

				const FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW |
					FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D | FMOD.MODE.LOOP_OFF;

				FMOD.RESULT r = _system.createSound(bytes, mode, ref ex, out FMOD.Sound sound);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod createSound failed for '{key}': {r}");
					return;
				}

				// Release a sound this key already held before overwriting, so the native
				// sample is not leaked.
				if (_sounds.TryGetValue(key, out FMOD.Sound existing))
					existing.release();
				_sounds[key] = sound;
			} catch (Exception ex) {
				Log.Error($"fmod register failed for '{key}': {ex}");
			}
		}

		public void PlayOneShot(string key, SoundParams parameters) {
			Play(key, parameters, false);
		}

		public Voice Play(string key, SoundParams parameters, bool loop) {
			if (!_available) return Voice.None;

			if (!_sounds.TryGetValue(key, out FMOD.Sound sound)) {
				Log.Warn($"fmod play of unregistered sound '{key}'");
				return Voice.None;
			}

			ReclaimFinished();
			Voice voice = _pool.Acquire();
			if (!voice.IsValid) return Voice.None;

			try {
				// Start paused so pan/volume/pitch are set before the first sample reaches the
				// mixer; otherwise the cue blips at centre pan and full volume for one buffer.
				FMOD.RESULT r = _system.playSound(sound, _master, true, out FMOD.Channel channel);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod playSound failed for '{key}': {r}");
					_pool.Release(voice);
					return Voice.None;
				}

				channel.setMode(loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);
				channel.setLoopCount(loop ? -1 : 0);
				Apply(channel, parameters.Clamped());
				channel.setPaused(false);

				_channels[voice.Slot] = channel;
				return voice;
			} catch (Exception ex) {
				Log.Error($"fmod play failed for '{key}': {ex}");
				_pool.Release(voice);
				return Voice.None;
			}
		}

		public void Update(Voice voice, SoundParams parameters) {
			if (!_available) return;
			if (!_pool.TryResolve(voice, out int slot)) return;
			Apply(_channels[slot], parameters.Clamped());
		}

		public void Stop(Voice voice) {
			if (!_available) return;
			if (!_pool.TryResolve(voice, out int slot)) return;
			_channels[slot].stop();
			_pool.Release(voice);
		}

		public void StopAll() {
			if (!_available) return;
			for (int slot = 0; slot < _pool.Capacity; slot++) {
				if (!_pool.IsActiveSlot(slot)) continue;
				_channels[slot].stop();
				_pool.ReleaseSlot(slot);
			}
		}

		// A finished one-shot leaves its Channel handle invalid with no Stop call, so before
		// each Acquire reclaim any slot whose channel has stopped. A looping voice reports
		// playing true until explicitly stopped, so it is not reclaimed here. isPlaying on a
		// finished or stolen channel returns an error result, which counts as not playing.
		private void ReclaimFinished() {
			for (int slot = 0; slot < _pool.Capacity; slot++) {
				if (!_pool.IsActiveSlot(slot)) continue;
				FMOD.RESULT r = _channels[slot].isPlaying(out bool playing);
				if (r != FMOD.RESULT.OK || !playing)
					_pool.ReleaseSlot(slot);
			}
		}

		// Channel-lifecycle results are deliberately not checked here: a one-shot can finish
		// between frames, after which setPan/setVolume/setPitch return ERR_INVALID_HANDLE.
		// That is expected, not a failure to log; the reclaim sweep frees the dead slot.
		private static void Apply(FMOD.Channel channel, SoundParams p) {
			channel.setPan(p.Pan);
			channel.setPitch(p.Pitch);
			channel.setVolume(p.Volume);
		}
	}
}
#endif
