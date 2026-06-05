using System;

namespace HandOfFateAccess.Util {
	/// <summary>
	/// Logging with a swappable backend. Defaults to Console so the offline
	/// test project can run without BepInEx or Unity. The plugin installs the
	/// BepInEx backend via SetBackend at startup. Every line is prefixed [HoFAccess].
	/// </summary>
	public static class Log {
		private const string Prefix = "[HoFAccess]";

		private static Action<string> _info = msg => Console.WriteLine(msg);
		private static Action<string> _warn = msg => Console.WriteLine(msg);
		private static Action<string> _error = msg => Console.Error.WriteLine(msg);
		private static Action<string> _debug = msg => Console.WriteLine(msg);

		/// <summary>
		/// Replace the logging sinks (e.g. route to the BepInEx logger in-game,
		/// or capture output in tests). Each line passed to a sink already carries
		/// the [HoFAccess] prefix and level marker.
		/// </summary>
		public static void SetBackend(Action<string> info, Action<string> warn, Action<string> error, Action<string> debug) {
			_info = info;
			_warn = warn;
			_error = error;
			_debug = debug;
		}

		public static void Debug(string msg) => _debug($"{Prefix} [DEBUG] {msg}");
		public static void Info(string msg) => _info($"{Prefix} {msg}");
		public static void Warn(string msg) => _warn($"{Prefix} {msg}");
		public static void Error(string msg) => _error($"{Prefix} {msg}");
	}
}
