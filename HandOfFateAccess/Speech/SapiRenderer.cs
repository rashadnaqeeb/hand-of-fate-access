using System;
using System.Runtime.InteropServices;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Speech {
	/// <summary>
	/// Renders text to PCM via SAPI through the vendored x86 HofSapi.dll, so the chance
	/// gambit can place a spoken card status in the stereo field and time it. Tolk can do
	/// neither: it only hands text to the active screen reader, with no buffer, pan, or
	/// start/stop. The native shim returns a 16-bit PCM WAV; Core's <see cref="WavAudio"/>
	/// decodes it into the float buffer the audio backend registers and plays panned.
	///
	/// HofSapi.dll must be preloaded from the plugins folder via NativeLoader before the
	/// first call, since [DllImport("HofSapi.dll")] resolves by name against loaded
	/// modules. Signatures match the shim's __cdecl exports.
	/// </summary>
	public sealed class SapiRenderer {
		[DllImport("HofSapi.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern int HofSapi_Init();

		[DllImport("HofSapi.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern void HofSapi_Shutdown();

		[DllImport("HofSapi.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern int HofSapi_Synthesize(
			[MarshalAs(UnmanagedType.LPWStr)] string text, out IntPtr bytes, out int len);

		private bool _initialized;
		private bool _available;

		public bool IsAvailable => _available;

		public bool Initialize() {
			if (_initialized) return _available;
			_initialized = true;

			try {
				int rc = HofSapi_Init();
				_available = rc == 0;
				if (_available)
					Log.Info("SAPI renderer initialized");
				else
					Log.Error($"HofSapi_Init failed (hr 0x{rc:X8})");
			} catch (Exception ex) {
				Log.Error(ex is DllNotFoundException ? $"HofSapi.dll not found: {ex}" : $"SAPI renderer init failed: {ex}");
				_available = false;
			}
			return _available;
		}

		public void Shutdown() {
			if (!_initialized) return;

			try {
				HofSapi_Shutdown();
			} catch (Exception ex) {
				Log.Warn($"SAPI renderer shutdown error: {ex}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		/// <summary>
		/// Renders <paramref name="text"/> to interleaved float PCM (mono). Returns false
		/// and logs if the renderer is unavailable or the native call fails; the out params
		/// are meaningful only when it returns true.
		/// </summary>
		public bool Render(string text, out float[] pcm, out int channels, out int sampleRate) {
			pcm = null;
			channels = 0;
			sampleRate = 0;
			if (!_available || string.IsNullOrEmpty(text)) return false;

			IntPtr buf = IntPtr.Zero;
			try {
				int rc = HofSapi_Synthesize(text, out buf, out int len);
				if (rc != 0 || buf == IntPtr.Zero || len <= 0) {
					Log.Warn($"HofSapi_Synthesize('{text}') failed (hr 0x{rc:X8}, len {len})");
					return false;
				}
				byte[] wav = new byte[len];
				Marshal.Copy(buf, wav, 0, len);
				WavAudio.Decode(wav, out pcm, out channels, out sampleRate);
				// Log the raw format so a stereo render (which Unity's panStereo would place
				// differently than a mono tone at the same pan) is visible, not guessed.
				Log.Info($"SAPI render '{text}': {channels}ch, {sampleRate}Hz, {pcm.Length} samples");
				// Fold to mono so panning matches the mono identity tones, then trim SAPI's
				// leading/trailing digital silence so the spoken word starts tight against its tone.
				pcm = Pcm.DownmixToMono(pcm, channels);
				channels = 1;
				pcm = Pcm.TrimSilence(pcm, 0.003f, (int)(0.005f * sampleRate));
				return true;
			} catch (Exception ex) {
				Log.Error($"SAPI render of '{text}' failed: {ex}");
				return false;
			} finally {
				// The shim allocates the buffer with CoTaskMemAlloc, so the COM task
				// allocator frees it across the boundary.
				if (buf != IntPtr.Zero) Marshal.FreeCoTaskMem(buf);
			}
		}
	}
}
