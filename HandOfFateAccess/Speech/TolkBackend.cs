using System;
using System.Runtime.InteropServices;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Speech {
	/// <summary>
	/// Speech backend over Tolk (x86), which routes to the active screen reader
	/// (NVDA/JAWS/SAPI). Tolk.dll and its client DLLs must be preloaded from the
	/// plugins folder via NativeLoader before Initialize, since [DllImport]
	/// resolves "Tolk.dll" by name against already-loaded modules.
	/// </summary>
	public class TolkBackend : ISpeechBackend {
		// Signatures match the vendored official Tolk C# binding exactly
		// (third_party/tolk/src/dotnet/Tolk.cs): every bool is a 1-byte C++
		// bool (UnmanagedType.I1) and strings are LPWStr. On x86 this matters
		// -- default 4-byte BOOL marshalling corrupts the call into the screen
		// reader and crashes the process.
		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern void Tolk_Load();

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern void Tolk_Unload();

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool Tolk_HasSpeech();

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern void Tolk_TrySAPI([MarshalAs(UnmanagedType.I1)] bool trySAPI);

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr Tolk_DetectScreenReader();

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool Tolk_Output(
			[MarshalAs(UnmanagedType.LPWStr)] string str,
			[MarshalAs(UnmanagedType.I1)] bool interrupt);

		[DllImport("Tolk.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		private static extern bool Tolk_Silence();

		private bool _initialized;
		private bool _available;

		public bool IsInitialized => _initialized;
		public bool IsAvailable => _available;

		public bool Initialize() {
			if (_initialized) return _available;

			try {
				Tolk_Load();
				Tolk_TrySAPI(true);
				_available = Tolk_HasSpeech();
				_initialized = true;

				if (_available) {
					IntPtr namePtr = Tolk_DetectScreenReader();
					string name = namePtr != IntPtr.Zero
						? Marshal.PtrToStringUni(namePtr)
						: "unknown";
					Log.Info($"Tolk backend initialized with: {name}");
				} else {
					Log.Warn("Tolk found no speech output");
				}

				return _available;
			} catch (Exception ex) {
				Log.Error(ex is DllNotFoundException ? $"Tolk.dll not found: {ex}" : $"Tolk init failed: {ex}");
				_initialized = true;
				_available = false;
				return false;
			}
		}

		public void Shutdown() {
			if (!_initialized) return;

			try {
				Tolk_Unload();
				Log.Info("Tolk backend shutdown");
			} catch (Exception ex) {
				Log.Warn($"Tolk shutdown error: {ex}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		public void Say(string text, bool interrupt) {
			if (!_available || string.IsNullOrEmpty(text)) return;

			try {
				if (!Tolk_Output(text, interrupt))
					Log.Warn("Tolk_Output returned false");
			} catch (Exception ex) {
				Log.Warn($"Tolk speech error: {ex}");
			}
		}

		public void Stop() {
			if (!_available) return;

			try {
				Tolk_Silence();
			} catch (Exception ex) {
				Log.Warn($"Tolk stop error: {ex}");
			}
		}
	}
}
