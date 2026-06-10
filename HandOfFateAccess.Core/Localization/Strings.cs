using System;
using System.Collections.Generic;

namespace HandOfFateAccess.Localization {
	/// <summary>
	/// The mod's authored strings in the player's language. The wording lives in
	/// <see cref="StringTable"/> (whose field initializers are the English source
	/// of truth) and one table per translated language in the registry below; this
	/// facade resolves every read against the active table at speech time, so a
	/// language switch is reflected live and no caller ever holds a stale value.
	/// The plugin drives <see cref="SetLanguage"/> off the game's own language
	/// setting; a language with no table speaks English.
	/// </summary>
	public static class Strings {
		// One factory per translated language, keyed by the game's language codes
		// (the 12 locales it ships). Each translation is an object initializer over
		// StringTable in Tables/StringTable.<Code>.cs; any field one leaves unset
		// keeps its English default, so a partial translation falls back per
		// string, not per language. LanguageTableTests sweeps every entry here.
		private static readonly Dictionary<string, Func<StringTable>> Tables =
			new Dictionary<string, Func<StringTable>>(StringComparer.OrdinalIgnoreCase) {
				{ "en", () => new StringTable() },
				{ "fr", StringTableFr.Create },
				{ "it", StringTableIt.Create },
				{ "de", StringTableDe.Create },
				{ "es", StringTableEs.Create },
				{ "pt-br", StringTablePtBr.Create },
				{ "ru", StringTableRu.Create },
				{ "hu", StringTableHu.Create },
				{ "uk", StringTableUk.Create },
				{ "ja", StringTableJa.Create },
				{ "ko", StringTableKo.Create },
				{ "zh", StringTableZh.Create },
			};

		private static StringTable _active = new StringTable();
		private static string _activeCode = "en";

		/// <summary>The code whose table is active ("en" after a fallback).</summary>
		public static string ActiveLanguage => _activeCode;

		/// <summary>
		/// Switch the authored strings to the table for <paramref name="code"/> (a
		/// game language code, matched case-insensitively). False when no table
		/// exists for it and English was selected instead.
		/// </summary>
		public static bool SetLanguage(string code) {
			if (!string.IsNullOrEmpty(code) && Tables.TryGetValue(code, out var factory)) {
				_active = factory();
				_activeCode = code.ToLowerInvariant();
				return true;
			}
			_active = new StringTable();
			_activeCode = "en";
			return false;
		}

		// Test seams: register a fake language, and restore the shipped registry
		// and English between tests (the registrations are tracked so a reset
		// never removes a real translation).
		private static readonly List<string> TestCodes = new List<string>();
		internal static void RegisterForTest(string code, Func<StringTable> factory) {
			Tables[code] = factory;
			TestCodes.Add(code);
		}
		internal static void ResetForTest() {
			foreach (string code in TestCodes)
				Tables.Remove(code);
			TestCodes.Clear();
			SetLanguage("en");
		}

		// The authored strings, resolved against the active language's table. What
		// each one is for, and the translator notes, live with the English wording
		// in StringTable.

		public static string ScreenLoading => _active.ScreenLoading;
		public static string ScreenIntro => _active.ScreenIntro;
		public static string ScreenAttract => _active.ScreenAttract;
		public static string ScreenMainMenu => _active.ScreenMainMenu;
		public static string ScreenDeckBuilder => _active.ScreenDeckBuilder;
		public static string ScreenDungeonSelect => _active.ScreenDungeonSelect;
		public static string ScreenMap => _active.ScreenMap;
		public static string ScreenCardTable => _active.ScreenCardTable;
		public static string ScreenEncounter => _active.ScreenEncounter;
		public static string ScreenCombat => _active.ScreenCombat;
		public static string ScreenShop => _active.ScreenShop;
		public static string ScreenDialogue => _active.ScreenDialogue;
		public static string ScreenInventory => _active.ScreenInventory;
		public static string ScreenPaused => _active.ScreenPaused;
		public static string ScreenResults => _active.ScreenResults;
		public static string ScreenCabinet => _active.ScreenCabinet;

		public static string CardCompleted => _active.CardCompleted;
		public static string CardNew => _active.CardNew;
		public static string CardPinned => _active.CardPinned;
		public static string CardCharge => _active.CardCharge;
		public static string CardCharges => _active.CardCharges;
		public static string CardLocked => _active.CardLocked;

		public static string DeckInsufficient => _active.DeckInsufficient;
		public static string DeckTooMany => _active.DeckTooMany;

		public static string ControlContinue => _active.ControlContinue;
		public static string TokenReward => _active.TokenReward;
		public static string AddToDeck => _active.AddToDeck;
		public static string SlotEmpty => _active.SlotEmpty;
		public static string CardToken => _active.CardToken;

		public static string ResourceHealth => _active.ResourceHealth;
		public static string ResourceMaxHealth => _active.ResourceMaxHealth;
		public static string ResourceFood => _active.ResourceFood;
		public static string ResourceGold => _active.ResourceGold;
		public static string ResourceIronOre => _active.ResourceIronOre;
		public static string ResourceTokens => _active.ResourceTokens;

		public static string StatusNoRun => _active.StatusNoRun;
		public static string ProgressScore => _active.ProgressScore;
		public static string ScorePlus => _active.ScorePlus;
		public static string ScoreTimes => _active.ScoreTimes;
		public static string ShopInsufficient => _active.ShopInsufficient;
		public static string ZoomReplacing => _active.ZoomReplacing;
		public static string CardFaceDown => _active.CardFaceDown;

		public static string MapSlotAttached => _active.MapSlotAttached;
		public static string MapCellHere => _active.MapCellHere;
		public static string MapCellReachable => _active.MapCellReachable;
		public static string MapCellLocked => _active.MapCellLocked;
		public static string MapCellOpen => _active.MapCellOpen;
		public static string MapExits => _active.MapExits;
		public static string DirUp => _active.DirUp;
		public static string DirDown => _active.DirDown;
		public static string DirLeft => _active.DirLeft;
		public static string DirRight => _active.DirRight;
		public static string MapRevealed => _active.MapRevealed;

		public static string BindingPressKey => _active.BindingPressKey;
		public static string BindingConflict => _active.BindingConflict;

		public static string GlossaryTitle => _active.GlossaryTitle;
		public static string GlossaryClosed => _active.GlossaryClosed;
		public static string GlossaryBlock => _active.GlossaryBlock;
		public static string GlossaryDodge => _active.GlossaryDodge;
		public static string GlossaryProjectile => _active.GlossaryProjectile;
		public static string GlossaryProjectileReflected => _active.GlossaryProjectileReflected;
		public static string GlossaryZonePrimed => _active.GlossaryZonePrimed;
		public static string GlossaryZoneArming => _active.GlossaryZoneArming;
		public static string GlossaryZoneActive => _active.GlossaryZoneActive;
		public static string GlossaryZoneInside => _active.GlossaryZoneInside;
		public static string GlossaryWallTones => _active.GlossaryWallTones;
		public static string GlossaryWallCollision => _active.GlossaryWallCollision;
		public static string GlossaryEnemyPing => _active.GlossaryEnemyPing;
		public static string GlossaryChest => _active.GlossaryChest;
		public static string GlossaryExit => _active.GlossaryExit;

		public static string SliderPercent(int percent) {
			return string.Format(_active.SliderPercentFormat, percent);
		}

		public static string GambitSlot(int number) {
			return string.Format(_active.GambitSlotFormat, number);
		}

		public static string PluginLoaded(string version) {
			return string.Format(_active.PluginLoadedFormat, version);
		}
	}
}
