using System;
using System.IO;
using System.Runtime.InteropServices;

namespace HandOfFateAccess.Util {
	/// <summary>
	/// Loads a native DLL by full path before its first P/Invoke, so a bare
	/// [DllImport("Name.dll")] resolves to the already-loaded module. Plugins
	/// live in BepInEx/plugins, which is not on the default DLL search path,
	/// so Tolk.dll (and the screen-reader client DLLs it loads by bare name)
	/// would not otherwise be found.
	/// </summary>
	public static class NativeLoader {
		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool SetDllDirectory(string lpPathName);

		/// <summary>
		/// Adds <paramref name="directory"/> to the process DLL search path and
		/// loads <paramref name="dllName"/> from it. Adding the directory lets
		/// Tolk.dll find its co-located screen-reader client DLLs, which it
		/// loads by bare name. Returns false (and logs) if the load fails.
		/// </summary>
		public static bool Preload(string directory, string dllName) {
			if (!SetDllDirectory(directory))
				Log.Warn($"SetDllDirectory failed for {directory} (win32 error {Marshal.GetLastWin32Error()}); Tolk may not find its co-located client DLLs");
			string fullPath = Path.Combine(directory, dllName);
			IntPtr handle = LoadLibrary(fullPath);
			if (handle == IntPtr.Zero) {
				Log.Error($"failed to load {dllName} from {directory} (win32 error {Marshal.GetLastWin32Error()})");
				return false;
			}
			Log.Info($"preloaded {dllName}");
			return true;
		}
	}
}
