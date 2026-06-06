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

		// Appended after a card's name when the game badges it as unseen ("new"), the same
		// badge a sighted player sees in the deck builder, shop and card choices. The badge
		// is an unlabelled sprite, so the word is authored here. Silence means already seen.
		public static readonly string CardNew = "new";

		// Appended after a card's name when it is pinned into the deck and so cannot be taken
		// out (the game blocks removal with a dialogue). Pinned shows as an unlabelled sprite,
		// so the words are authored here. Silence means the card can be removed.
		public static readonly string CardPinned = "cannot remove";

		// The ability-use counter on an artifact or consumable equipment card ("3 charges"),
		// the number a sighted player reads off the card's counter. The counter is a bare
		// number with no label, so the noun is authored here. Singular/plural per count.
		public static readonly string CardCharge = "charge";
		public static readonly string CardCharges = "charges";

		// Deck-builder pile status, appended after a pile's count when the deck is off its
		// required limit ("Equipment, 8/12, insufficient" / "Encounter, 15/12, too many").
		// The game flags this only with coloured icons (a tick, a cross) and no text, so the
		// words are authored here. A pile exactly on its limit reads just the count, with no
		// status word, since the equal count already conveys it.
		public static readonly string DeckInsufficient = "insufficient";
		public static readonly string DeckTooMany = "too many";

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

		// Card zoom (a single card presented for a decision: examine, equip, buy, keep,
		// reveal). "replacing" joins the zoomed item to the equipped item a replace would
		// swap out ("Iron Sword, damage 5, replacing Rusty Dagger, damage 3"); the item
		// text and the decision prompt come from the game, only this connective is authored.
		public static readonly string ZoomReplacing = "replacing";

		// Spoken in place of a face-down card's identity, withheld until the player reveals
		// it (matching the card back a sighted player sees). Used for a flipped card in a
		// zoom and for a face-down quest/special encounter on the map.
		public static readonly string CardFaceDown = "face down card";

		// Map slot. Joins an encounter to a card stacked under it by a spice event (a
		// supply/equipment/pain reward or hazard attached to that encounter), which sits
		// offset and peeks out on the board: "Goblins, token available, with Apple". The
		// attached card's own name and text come from the game; only this connective is
		// authored.
		public static readonly string MapSlotAttached = "with";

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
