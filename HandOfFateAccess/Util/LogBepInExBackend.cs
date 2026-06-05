using BepInEx.Logging;

namespace HandOfFateAccess.Util {
	/// <summary>
	/// Routes Log through BepInEx's ManualLogSource (which also mirrors to the
	/// Unity player log / output_log.txt). Separated from Log so BepInEx types
	/// are never resolved during offline test runs.
	/// </summary>
	internal static class LogBepInExBackend {
		public static void Install(ManualLogSource source) {
			Log.SetBackend(
				info: msg => source.LogInfo(msg),
				warn: msg => source.LogWarning(msg),
				error: msg => source.LogError(msg),
				debug: msg => source.LogDebug(msg));
		}
	}
}
