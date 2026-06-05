namespace HandOfFateAccess.Localization {
	/// <summary>
	/// The single home for every word the mod itself authors and speaks. Nothing
	/// the player hears should be an inline string literal anywhere else; it lives
	/// here so the whole authored surface can be translated in one place later.
	///
	/// TRANSLATOR NOTES (read before editing):
	/// - Translate the string VALUES only. Leave the field names alone.
	/// - These are read aloud by a screen reader to a blind player, with no visual
	///   fallback. Keep them short and information-dense; do not add politeness,
	///   framing, or extra words the original omits.
	/// - "{0}" is a placeholder the mod fills in at runtime. Keep it exactly as
	///   written; you may move it within the sentence to fit the target language's
	///   word order. The comment on each format string says what it stands for.
	/// - Text the player reads off the game itself (card names, descriptions, costs)
	///   is NOT here -- the game's own localization already covers it.
	/// </summary>
	public static class Strings {

		// Screen titles. Spoken on their own the moment the player moves into a major
		// game context (a menu, the map, combat, a shop), so each must read as a
		// standalone label. Short title-style noun phrases.
		public static readonly string ScreenLoading = "Loading";
		public static readonly string ScreenIntro = "Intro";
		public static readonly string ScreenAttract = "Attract";
		public static readonly string ScreenMainMenu = "Main menu";
		public static readonly string ScreenDeckBuilder = "Deck builder";
		public static readonly string ScreenDungeonSelect = "Dungeon select";
		public static readonly string ScreenMap = "Map";
		public static readonly string ScreenCardTable = "Card table";
		public static readonly string ScreenEncounter = "Encounter";
		public static readonly string ScreenCombat = "Combat";
		public static readonly string ScreenShop = "Shop";
		public static readonly string ScreenDialogue = "Dialogue";
		public static readonly string ScreenInventory = "Inventory";
		public static readonly string ScreenPaused = "Paused";
		public static readonly string ScreenResults = "Results";
		public static readonly string ScreenCabinet = "Cabinet";

		// Card status. Appended after a card's name when that card or encounter has
		// already been finished, e.g. "Bandit Camp, completed".
		public static readonly string CardCompleted = "completed";

		// Token stakes: the cards a card will grant or take from the player. {0} is
		// the name of that card. English puts the verb first ("gain Gold"); reorder
		// to fit the target language.
		public static string TokenGain(string cardName) {
			return string.Format("gain {0}", cardName);
		}

		public static string TokenLose(string cardName) {
			return string.Format("lose {0}", cardName);
		}

		// Plugin lifecycle. Spoken once at startup to confirm the accessibility mod
		// is running. {0} is the mod version (e.g. "0.1.0"). The product name is part
		// of this string and IS meant to be translated: render "Hand of Fate Access"
		// using the game's own official localized title for "Hand of Fate" in the
		// target language, followed by that language's word for "Access".
		public static string PluginLoaded(string version) {
			return string.Format("Hand of Fate Access version {0} loaded", version);
		}
	}
}
