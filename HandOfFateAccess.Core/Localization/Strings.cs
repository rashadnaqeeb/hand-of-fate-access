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

		// Appended after an archetype's (Fates) name when it is not yet unlocked: a darkened
		// card whose action is buy rather than continue. The lock shows as a darkened card and
		// sprite with no text, so the word is authored here. Silence means it is available.
		public static readonly string CardLocked = "locked";

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

		// The end-of-run reward token: a 3D gem prop the player activates to unlock the
		// cards it grants. It carries no label, and a sighted player cannot read its reward
		// either until they activate it and flip the cards it deals, so it announces only as
		// a collectible token; the rewards are discovered through the per-card reveal as each
		// face-down card is flipped. The game has no localized noun for it, so it is here.
		public static readonly string TokenReward = "reward token";

		// The "add to deck" button on the reward screen, which adds the revealed reward cards
		// to the player's collection. The button is a label-less sprite, so the action word
		// is authored here; the banner above it (what is being added) comes from the game.
		public static readonly string AddToDeck = "add to deck";

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

		// Spoken when the player asks for the resource status (the status key) while no
		// run is live, so no stat cards are on the table to read. Keeps the key from
		// being silent in menus and between runs. The game has no such string of its
		// own, so it is authored here.
		public static readonly string StatusNoRun = "No run in progress";

		// The score noun in the dungeon-progress readout ("1200 score"). The HUD shows the
		// score as a bare number beside an icon with no word, so the noun is authored here.
		// The accompanying level word is reused from the game's own SCORE_LEVEL string.
		public static readonly string ProgressScore = "score";

		// The end-of-run scoreboard draws each entry's points as a symbol ("x2" for a
		// multiplier, "+50" for a bonus). A screen reader voices the "x" as the letter and may
		// drop the "+", so the symbol is spoken as a word instead; the number comes from the
		// game. A row's total has no symbol and reads as the bare number.
		public static readonly string ScorePlus = "plus";
		public static readonly string ScoreTimes = "times";

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

		// A map card's state word, spoken after its card by the free-roam cursor. "here"
		// marks the player's own slot; "reachable" an unlocked move orthogonally adjacent to
		// the player; "locked" a slot still sealed; "open" an unlocked slot not adjacent, so
		// not a move yet. The completed state reuses CardCompleted. The game shows these as
		// highlight/darken/lock sprites with no text.
		public static readonly string MapCellHere = "here";
		public static readonly string MapCellReachable = "reachable";
		public static readonly string MapCellLocked = "locked";
		public static readonly string MapCellOpen = "open";

		// The directions the cursor can step from a card (where a neighbouring card sits),
		// e.g. "exits up, right". The lead word and the four directions (matching the
		// movement keys) are authored here; the game shows the board shape only visually.
		public static readonly string MapExits = "exits";
		public static readonly string DirUp = "up";
		public static readonly string DirDown = "down";
		public static readonly string DirLeft = "left";
		public static readonly string DirRight = "right";

		// Leads each map card flipped face-up by a reveal effect (Explorer's Helmet, an
		// encounter's reveal reward...): "revealed Goblins, ...". Repeated per card so the
		// boundary between several revealed cards is unmistakable. The game shows the
		// reveal only as a camera move and flip animation, with no text.
		public static readonly string MapRevealed = "revealed";

		// The chance gambit's spoken card statuses are NOT authored here: the game has its
		// own localized outcome titles (CHANCE_TITLE_SUCCESS and friends), read live through
		// Localization.Localize in GambitStatusSpeech so they speak in the player's language.

		// A settings slider's position (the volume sliders), spoken as a percentage
		// ("70%"). The slider draws its value only as a fill sprite with no text, so
		// the wording is authored here. {0} is the percentage number, 0 to 100.
		public static string SliderPercent(int percent) {
			return string.Format("{0}%", percent);
		}

		// Key-binding rows on the controls screen. "press a key" is spoken when a
		// rebind starts listening for the new key, which the game shows only as a
		// colour change on the button. "conflict" is appended after a binding's key
		// when the game flags the row invalid (the same key bound to two actions),
		// which it shows only as a red tint.
		public static readonly string BindingPressKey = "press a key";
		public static readonly string BindingConflict = "conflict";

		// A chance card's current left-to-right position while picking, spoken in place of its
		// hidden "face down card" identity so the player can navigate to the slot they tracked
		// by ear. {0} is the 1-based slot number. The shuffled face-down cards are otherwise
		// indistinguishable, so position is the one thing worth saying. The game has no such
		// label, so it is authored here.
		public static string GambitSlot(int number) {
			return string.Format("Slot {0}", number);
		}

		// The sound glossary: a speech-only list of the mod's combat sounds, opened from
		// the mod's own option in the pause menu, so the player can learn or re-check
		// what each sound means outside a fight. The title is both the injected pause
		// option's caption and the line spoken on open; the closed line is spoken on a
		// deliberate close back to the pause list (the pause menu closing under the
		// glossary closes it silently).
		public static readonly string GlossaryTitle = "Sound glossary";
		public static readonly string GlossaryClosed = "Glossary closed";

		// Glossary entries, one per combat sound: the sound's name first (the distinguishing
		// word), then what hearing it means in a fight. Spoken as the player arrows through
		// the list; activating an entry plays the sound itself.
		public static readonly string GlossaryBlock = "Block, attack you can block, or reflect if ranged";
		public static readonly string GlossaryDodge = "Dodge, attack you must dodge, also a hazard launching";
		public static readonly string GlossaryProjectile = "Projectile, shot in flight";
		public static readonly string GlossaryProjectileReflected = "Reflected projectile, your shot flying back at an enemy";
		public static readonly string GlossaryZonePrimed = "Zone primed, idle trap, approaching fires it";
		public static readonly string GlossaryZoneArming = "Zone arming, hazard forming, damage not on yet";
		public static readonly string GlossaryZoneActive = "Zone active, area that hurts to stand in";
		public static readonly string GlossaryZoneInside = "Zone inside, you are in a hazard, move away from the sound";
		public static readonly string GlossaryWallTones = "Wall tones, walls right, left, top, bottom";
		public static readonly string GlossaryWallCollision = "Wall collision, walked into a wall";
		public static readonly string GlossaryEnemyPing = "Enemy ping, nearest enemy, answers the locator key";
		public static readonly string GlossaryChest = "Chest beacon, walk to it to open the chest";
		public static readonly string GlossaryExit = "Exit beacon, walk to it to finish the level, also ends a boss fight";

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
