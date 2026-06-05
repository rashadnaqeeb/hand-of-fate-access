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

		// Readouts for focusable controls that carry no label of their own, so the
		// raw object name would otherwise be spoken. The game has no single reusable
		// localized "continue" label (only per-encounter ones), so it is authored here.
		public static readonly string ControlContinue = "Continue";

		// Appended after an equipment slot's category (e.g. "Weapons, empty") when the
		// paperdoll slot holds no equipped card. An empty slot is sprite-only with no
		// label, and the game has no localized "empty slot" string, so it is authored
		// here; the category name itself comes from the game's own localization.
		public static readonly string SlotEmpty = "empty";

		// Shown on an encounter card that offers a token to win (the game draws a token
		// gem on the card). The cards the token grants or removes are not displayed to
		// the player anywhere, so only the presence of a winnable token is announced,
		// matching what the gem conveys.
		public static readonly string CardToken = "token available";

		// Resource nouns, spoken in the on-demand resource readout ("200/200 health, 5
		// food, 3 gold") and in live change announcements ("-5 health", "+3 gold"). The
		// game's own STAT_ keys are format strings ("Gold: {0}"), not bare nouns, so the
		// nouns are authored here. Kept short: these read aloud often.
		public static readonly string ResourceHealth = "health";
		public static readonly string ResourceMaxHealth = "max health";
		public static readonly string ResourceFood = "food";
		public static readonly string ResourceGold = "gold";
		public static readonly string ResourceIronOre = "iron ore";
		public static readonly string ResourceTokens = "tokens";

		// The score noun in the dungeon-progress readout ("1200 score"). The HUD shows the
		// score as a bare number beside an icon with no word, so the noun is authored here.
		// The accompanying level word is reused from the game's own SCORE_LEVEL string.
		public static readonly string ProgressScore = "score";

		// The equipment-replace prompt. When picked-up gear would replace something you
		// have equipped, the game shows a new-vs-old comparison on display-only panels the
		// focus model never reaches, with a confirm/cancel (no navigable buttons). The
		// decision line is spoken first, then the hint queues behind it so acting quickly
		// skips it. {0} is the new item, {1} the equipped item it would replace, each with
		// its own stats. The framing is authored; the item text comes from the live panels.
		public static string EquipReplace(string newItem, string oldItem) {
			return string.Format("Equip {0}, replacing {1}", newItem, oldItem);
		}

		// Spoken after the replace decision line; confirm equips the new item, cancel keeps
		// the currently equipped one (the new item is stowed in reserve).
		public static readonly string EquipReplaceHint = "confirm to equip, cancel to keep";

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
