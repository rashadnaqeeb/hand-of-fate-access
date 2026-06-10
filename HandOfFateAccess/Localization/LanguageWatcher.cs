using HandOfFateAccess.Util;

namespace HandOfFateAccess.Localization {
	/// <summary>
	/// Keeps the authored strings table on the game's text language. Edge-polled
	/// from the pump: the game seeds its language late (GameState_Init reads the
	/// profile's saved choice well after our startup) and the settings screen can
	/// switch it at any time, so the live value is re-read each frame and acted on
	/// only when it changes.
	/// </summary>
	internal sealed class LanguageWatcher {
		private string _seen;

		public void Pump() {
			// global:: to reach the game's Localization class; the mod's own
			// HandOfFateAccess.Localization namespace otherwise shadows it here.
			// isActive instead of instance: the instance getter creates the game's
			// singleton when it is missing, which is not ours to do.
			if (!global::Localization.isActive)
				return;
			string code = global::Localization.instance.currentLanguage;
			if (string.IsNullOrEmpty(code) || code == _seen)
				return;
			_seen = code;
			if (Strings.SetLanguage(code))
				Log.Info($"language '{code}'");
			else
				Log.Info($"language '{code}' has no authored-string translation; speaking English");
		}
	}
}
