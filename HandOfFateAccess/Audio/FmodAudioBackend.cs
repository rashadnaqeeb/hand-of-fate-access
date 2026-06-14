using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The non-speech audio backend: an FMOD Core <c>System</c> driving a pool of voices.
	/// FMOD is used for three things Unity's mixer denied: a flat stereo pan law
	/// (<c>Channel.setPan(-1..1)</c> places a cue fully in one ear with no constant-power
	/// curve), much lower trigger latency (a small DSP buffer set before init, versus
	/// Unity's ~200ms), and no positional postprocessing applied behind our back. The
	/// spatial grammar Core computes is the one the player hears.
	///
	/// It needs the license-gated FMOD SDK, vendored locally (see third_party/fmod): the
	/// official <c>fmod.cs</c> binding (compiled in by the csproj) and the native x86
	/// <c>fmod.dll</c> (preloaded from the plugins folder by NativeLoader before the first
	/// call, exactly as Tolk and the SAPI shim are).
	///
	/// One FMOD System runs alongside the System the game's own engine (Unity 5.3 is FMOD
	/// under the hood) already owns. The default output is the shared OS mixer, which does
	/// not take the device exclusively, so the two coexist; init logs the output so a
	/// device conflict is visible rather than guessed. Slot bookkeeping is the shared
	/// engine-agnostic <see cref="VoicePool"/>; this class binds slots to FMOD Channels and
	/// reclaims slots whose one-shot has finished.
	/// </summary>
	public sealed class FmodAudioBackend : IAudioBackend {
		// Every concurrent voice draws a slot: up to 32 projectile/mover synth voices, the 4 wall
		// tones, and the zone, beacon, attack, recharge, locator and glossary cues on top. Sized
		// well above that peak so a busy fight never exhausts the pool. FMOD's own software
		// channel limit is set higher again at init.
		private const int VoiceCount = 64;

		// A small DSP buffer trades a little dropout headroom for low output latency, the
		// whole reason for leaving Unity. 512 frames x 4 buffers is a conservative low
		// value (~40ms of mix-ahead, one buffer of which is the added trigger latency);
		// must be set before System.init. Tune down further if cues still feel late.
		private const uint DspBufferLength = 512;
		private const int DspBufferCount = 4;

		// FMOD plugin SDK version a custom DSP must declare (FMOD_PLUGIN_SDK_VERSION).
		private const uint PluginSdkVersion = 110;
		// Max frames pulled from a source per Fill call; sources size their scratch to this. The DSP
		// read runs at the mixer block (DspBufferLength), well under this, so the fill loop is just
		// defensive. The 8x covers any surround output channel count.
		private const int SynthChunkFrames = 1024;
		private const int SynthScratchFloats = SynthChunkFrames * 8;

		private FMOD.System _system;
		private FMOD.ChannelGroup _master;
		private readonly FMOD.Channel[] _channels = new FMOD.Channel[VoiceCount];
		private readonly Dictionary<string, FMOD.Sound> _sounds = new Dictionary<string, FMOD.Sound>();
		// Synth voices are generator DSPs played on a channel, the mixer-thread path that scales to
		// many concurrent real-time voices (a user stream, serviced by one buffered thread, glitches
		// past a couple of voices). The DSPs and their read callbacks are kept alive by key: the
		// callback's function pointer is held by native FMOD, so the GC must not collect the delegate.
		private readonly Dictionary<string, FMOD.DSP> _synthDsps = new Dictionary<string, FMOD.DSP>();
		private readonly Dictionary<string, FMOD.DSP_READ_CALLBACK> _synthReads =
			new Dictionary<string, FMOD.DSP_READ_CALLBACK>();
		// A synth callback cannot log (audio thread); it parks messages here for Pump to log.
		private volatile string _synthFault;
		private volatile string _synthDiag;
		private bool _synthDiagLogged;
		private int _mixRate;
		private bool _outputMono;
		private VoicePool _pool;
		private bool _initialized;
		private bool _available;

		public bool IsInitialized => _initialized;
		public bool IsAvailable => _available;
		public int OutputSampleRate => _mixRate;
		public bool IsOutputMono => _outputMono;

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
				// The mixer rate: synth sources generate at this so FMOD resamples nothing and the
				// output matches what the old device-rate synth produced. The speaker mode drives
				// the mono-output warning.
				_system.getSoftwareFormat(out _mixRate, out FMOD.SPEAKERMODE speakerMode, out _);
				_outputMono = speakerMode == FMOD.SPEAKERMODE.MONO;
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
				$"output {output}, {_mixRate}Hz, dsp buffer {blockLen}x{blockCount}");
		}

		public void Pump() {
			if (!_available) return;
			// Surface any fault a synth callback parked since last frame (it cannot log itself).
			string fault = _synthFault;
			if (fault != null) {
				_synthFault = null;
				Log.Warn("fmod synth callback faulted (" + fault + ")");
			}
			string diag = _synthDiag;
			if (diag != null) {
				_synthDiag = null;
				Log.Debug("fmod " + diag);
			}
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
					// Release the synth DSPs and drop their rooted callbacks.
					foreach (FMOD.DSP dsp in _synthDsps.Values)
						dsp.release();
					_synthDsps.Clear();
					_synthReads.Clear();
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

		public void RegisterSynth(string key, IPcmSource source, int channels, int sampleRate) {
			if (!_available) return;

			try {
				// Per-source scratch, allocated here on the main thread; the audio callback never
				// allocates. Flat-sized so it covers any output channel count.
				float[] scratch = new float[SynthScratchFloats];
				FMOD.DSP_READ_CALLBACK read =
					(ref FMOD.DSP_STATE state, IntPtr inbuf, IntPtr outbuf, uint length, int inch, ref int outch) =>
						SynthRead(key, source, scratch, outbuf, length, outch);

				// A generator DSP: no input buffers, one output buffer the read callback fills. It
				// runs on the mixer thread at the DSP block, the right place for many concurrent
				// real-time voices.
				FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
				desc.pluginsdkversion = PluginSdkVersion;
				desc.name = new byte[32];
				byte[] label = System.Text.Encoding.ASCII.GetBytes("HoFSynth");
				Array.Copy(label, desc.name, label.Length);
				desc.version = 1;
				desc.numinputbuffers = 0;
				desc.numoutputbuffers = 1;
				desc.read = read;

				FMOD.RESULT r = _system.createDSP(ref desc, out FMOD.DSP dsp);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod createDSP (synth) failed for '{key}': {r}");
					return;
				}

				// Pin the DSP's channel count. A mono source (1) is panned by the playing channel
				// (FMOD's mono pan law is equal-power, the placement the mod uses); a source that
				// pans itself into stereo (2, the gambit) plays through a neutral channel. Left to
				// default, a generator can present a count that bypasses the channel panner.
				FMOD.SPEAKERMODE mode = channels == 1 ? FMOD.SPEAKERMODE.MONO : FMOD.SPEAKERMODE.STEREO;
				dsp.setChannelFormat((FMOD.CHANNELMASK)0, channels, mode);

				if (_synthDsps.TryGetValue(key, out FMOD.DSP existing))
					existing.release();
				_synthDsps[key] = dsp;
				_synthReads[key] = read;
			} catch (Exception ex) {
				Log.Error($"fmod register synth failed for '{key}': {ex}");
			}
		}

		// The generator DSP read callback: FMOD's mixer asks for `length` frames of `channels`
		// (outch) interleaved float output. Pull them from the source into the managed scratch and
		// copy into FMOD's native output buffer, in scratch-sized chunks so the whole block is
		// filled. Runs on the mixer thread, so it never logs or allocates, and never lets an
		// exception cross back into native FMOD: on fault it fills the remainder with silence and
		// parks the message for Pump to log.
		private FMOD.RESULT SynthRead(string key, IPcmSource source, float[] scratch, IntPtr outbuffer, uint length, int channels) {
			int totalFloats = (int)length * channels;
			if (!_synthDiagLogged) {
				_synthDiagLogged = true;
				_synthDiag = $"synth dsp: {length} frames, {channels}ch, scratch {scratch.Length}";
			}
			int done = 0;
			try {
				while (done < totalFloats) {
					int frames = (totalFloats - done) / channels;
					if (frames > SynthChunkFrames) frames = SynthChunkFrames;
					int n = frames * channels;
					source.Fill(scratch, channels, frames);
					Marshal.Copy(scratch, 0, new IntPtr(outbuffer.ToInt64() + done * sizeof(float)), n);
					done += n;
				}
			} catch (Exception ex) {
				_synthFault = key + ": " + ex.Message;
				// Fill whatever is left with silence rather than leave stale buffer noise.
				Array.Clear(scratch, 0, scratch.Length);
				while (done < totalFloats) {
					int n = totalFloats - done;
					if (n > scratch.Length) n = scratch.Length;
					Marshal.Copy(scratch, 0, new IntPtr(outbuffer.ToInt64() + done * sizeof(float)), n);
					done += n;
				}
			}
			return FMOD.RESULT.OK;
		}

		public void PlayOneShot(string key, SoundParams parameters) {
			Play(key, parameters, false);
		}

		public Voice Play(string key, SoundParams parameters, bool loop) {
			if (!_available) return Voice.None;

			// A synth voice is a generator DSP; a regular cue is a registered sound. Both end up as
			// a paused channel on the master group, configured then unpaused below.
			bool isSynth = _synthDsps.TryGetValue(key, out FMOD.DSP dsp);
			bool isSound = !isSynth && _sounds.ContainsKey(key);
			if (!isSynth && !isSound) {
				Log.Warn($"fmod play of unregistered sound '{key}'");
				return Voice.None;
			}

			ReclaimFinished();
			Voice voice = _pool.Acquire();
			if (!voice.IsValid) return Voice.None;

			try {
				// Start paused so pan/volume/pitch are set before the first sample reaches the
				// mixer; otherwise the cue blips at centre pan and full volume for one buffer.
				FMOD.Channel channel;
				FMOD.RESULT r = isSynth
					? _system.playDSP(dsp, _master, true, out channel)
					: _system.playSound(_sounds[key], _master, true, out channel);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod play failed for '{key}': {r}");
					_pool.Release(voice);
					return Voice.None;
				}

				// A generator DSP runs until its channel is stopped, so it needs no loop mode; a
				// sound loops per the flag. The source applies its own pan/volume, so a synth voice
				// plays through a neutral channel.
				if (!isSynth) {
					channel.setMode(loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);
					channel.setLoopCount(loop ? -1 : 0);
				}
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
