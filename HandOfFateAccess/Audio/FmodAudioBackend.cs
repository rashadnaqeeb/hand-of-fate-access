using System;
using System.Runtime.InteropServices;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The non-speech audio backend: an FMOD Core <c>System</c> hosting Core's one
	/// <see cref="AudioMixer"/> as a generator DSP on the mixer thread. FMOD is used
	/// for two things Unity's mixer denied: much lower trigger latency (a small DSP
	/// buffer set before init, versus Unity's ~200ms) and no positional
	/// postprocessing applied behind our back - every audible decision (pan law,
	/// interaural delay, placement pitch, mixing) is Core's, and what Core renders is
	/// what the player hears.
	///
	/// It needs the license-gated FMOD SDK, vendored locally (see third_party/fmod):
	/// the official <c>fmod.cs</c> binding (compiled in by the csproj) and the native
	/// x86 <c>fmod.dll</c> (preloaded from the plugins folder by NativeLoader before
	/// the first call, exactly as Tolk and the SAPI shim are).
	///
	/// One FMOD System runs alongside the System the game's own engine (Unity 5.3 is
	/// FMOD under the hood) already owns. The default output is the shared OS mixer,
	/// which does not take the device exclusively, so the two coexist; init logs the
	/// output so a device conflict is visible rather than guessed.
	/// </summary>
	public sealed class FmodAudioBackend : IAudioBackend {
		// A small DSP buffer trades a little dropout headroom for low output latency,
		// the whole reason for leaving Unity. 512 frames x 4 buffers is a conservative
		// low value (~40ms of mix-ahead, one buffer of which is the added trigger
		// latency); must be set before System.init. Tune down further if cues still
		// feel late.
		private const uint DspBufferLength = 512;
		private const int DspBufferCount = 4;

		// FMOD plugin SDK version a custom DSP must declare (FMOD_PLUGIN_SDK_VERSION).
		private const uint PluginSdkVersion = 110;
		// Max frames pulled from the mixer per Fill call; the scratch is sized to this.
		// The DSP read runs at the mixer block (DspBufferLength), well under it, so the
		// fill loop is just defensive. The 8x covers any surround output channel count.
		private const int ChunkFrames = 1024;
		private const int ScratchFloats = ChunkFrames * 8;

		private FMOD.System _system;
		private FMOD.ChannelGroup _master;
		// The hosted mixer runs as one generator DSP on one channel for the whole
		// session. The read callback's function pointer is held by native FMOD, so the
		// delegate is kept alive here or the GC would collect it under FMOD's feet.
		private FMOD.DSP _mixerDsp;
		private FMOD.Channel _mixerChannel;
		private FMOD.DSP_READ_CALLBACK _mixerRead;
		private bool _hosting;
		// The read callback cannot log (audio thread); it parks messages here for Pump.
		private volatile string _mixerFault;
		private volatile string _mixerDiag;
		private bool _mixerDiagLogged;
		private int _mixRate;
		private bool _outputMono;
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

				r = _system.init(8, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod System.init failed: {r}");
					_system.release();
					_available = false;
					return false;
				}

				_system.getMasterChannelGroup(out _master);
				// The mixer rate: Core generates and spatializes at this so FMOD
				// resamples nothing. The speaker mode drives the mono-output warning.
				_system.getSoftwareFormat(out _mixRate, out FMOD.SPEAKERMODE speakerMode, out _);
				_outputMono = speakerMode == FMOD.SPEAKERMODE.MONO;
				_available = true;

				LogDeviceInfo();
				Log.Info("fmod audio backend initialized");
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

		// Surface the running output and version so a clash with the game's own FMOD
		// System (or a silent fallback to the NOSOUND output) is visible in the log,
		// not a mystery. Kept to calls whose binding signatures are stable across the
		// FMOD 2.0x line.
		private void LogDeviceInfo() {
			_system.getVersion(out uint version);
			_system.getOutput(out FMOD.OUTPUTTYPE output);
			_system.getDSPBufferSize(out uint blockLen, out int blockCount);
			Log.Info($"fmod {version >> 16}.{(version >> 8) & 0xFF:D2}.{version & 0xFF:D2}, " +
				$"output {output}, {_mixRate}Hz, dsp buffer {blockLen}x{blockCount}");
		}

		public bool Host(IPcmSource mixer) {
			if (!_available) return false;
			if (_hosting) return true;

			try {
				// The mixer's scratch, allocated here on the main thread; the audio
				// callback never allocates.
				float[] scratch = new float[ScratchFloats];
				_mixerRead =
					(ref FMOD.DSP_STATE state, IntPtr inbuf, IntPtr outbuf, uint length, int inch, ref int outch) =>
						MixerRead(mixer, scratch, outbuf, length, outch);

				// A generator DSP: no input buffers, one output buffer the read callback
				// fills. It runs on the mixer thread at the DSP block, pinned stereo so
				// Core's placement reaches the output untouched.
				FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
				desc.pluginsdkversion = PluginSdkVersion;
				desc.name = new byte[32];
				byte[] label = System.Text.Encoding.ASCII.GetBytes("HoFMixer");
				Array.Copy(label, desc.name, label.Length);
				desc.version = 1;
				desc.numinputbuffers = 0;
				desc.numoutputbuffers = 1;
				desc.read = _mixerRead;

				FMOD.RESULT r = _system.createDSP(ref desc, out _mixerDsp);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod createDSP (mixer) failed: {r}");
					_available = false;   // no output = no audio; features must see it
					return false;
				}
				_mixerDsp.setChannelFormat((FMOD.CHANNELMASK)0, 2, FMOD.SPEAKERMODE.STEREO);

				// One channel, playing for the whole session; the mixer emits silence
				// when nothing sounds. Unpaused immediately: there are no per-sound
				// parameters to set, so there is no first-buffer blip to avoid.
				r = _system.playDSP(_mixerDsp, _master, false, out _mixerChannel);
				if (r != FMOD.RESULT.OK) {
					Log.Error($"fmod playDSP (mixer) failed: {r}");
					_mixerDsp.release();
					_available = false;
					return false;
				}
				_hosting = true;
				return true;
			} catch (Exception ex) {
				Log.Error($"fmod mixer host failed: {ex}");
				_available = false;
				return false;
			}
		}

		// The generator DSP read callback: FMOD's mixer thread asks for `length`
		// frames of `outch` interleaved float output. Pull them from Core's mixer into
		// the managed scratch and copy into FMOD's native output buffer, in
		// scratch-sized chunks so the whole block is filled. Never logs or allocates,
		// and never lets an exception cross back into native FMOD: on fault it fills
		// the remainder with silence and parks the message for Pump to log.
		private FMOD.RESULT MixerRead(IPcmSource mixer, float[] scratch, IntPtr outbuffer, uint length, int channels) {
			int totalFloats = (int)length * channels;
			if (!_mixerDiagLogged) {
				_mixerDiagLogged = true;
				_mixerDiag = $"mixer dsp: {length} frames, {channels}ch, scratch {scratch.Length}";
			}
			int done = 0;
			try {
				while (done < totalFloats) {
					int frames = (totalFloats - done) / channels;
					if (frames > ChunkFrames) frames = ChunkFrames;
					int n = frames * channels;
					mixer.Fill(scratch, channels, frames);
					Marshal.Copy(scratch, 0, new IntPtr(outbuffer.ToInt64() + done * sizeof(float)), n);
					done += n;
				}
			} catch (Exception ex) {
				_mixerFault = ex.Message;
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

		public void Pump() {
			if (!_available) return;
			// Surface any fault the mixer callback parked since last frame (it cannot
			// log itself).
			string fault = _mixerFault;
			if (fault != null) {
				_mixerFault = null;
				Log.Warn("fmod mixer callback faulted (" + fault + ")");
			}
			string diag = _mixerDiag;
			if (diag != null) {
				_mixerDiag = null;
				Log.Debug("fmod " + diag);
			}
			// FMOD requires one System.update per frame for housekeeping.
			_system.update();
		}

		public void Shutdown() {
			if (!_initialized) return;

			try {
				if (_available) {
					if (_hosting) {
						_mixerChannel.stop();
						_mixerDsp.release();
						_hosting = false;
						_mixerRead = null;
					}
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
	}
}
