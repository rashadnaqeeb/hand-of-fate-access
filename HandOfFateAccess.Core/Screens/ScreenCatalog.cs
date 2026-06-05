using System.Collections.Generic;
using HandOfFateAccess.Localization;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// The single place every screen's spoken name lives, plus whether entering it
	/// is worth announcing. Mod-authored wording (the game's own state names are
	/// developer-internal). Transient states -- loading, the splash/intro/attract
	/// sequence, level in/out transitions -- collapse to non-announcing ids so the
	/// player hears the destination context, not the machinery getting there.
	/// Pure and testable; no engine types.
	/// </summary>
	public static class ScreenCatalog {
		private sealed class Entry {
			public readonly string Name;
			public readonly bool Announce;
			public Entry(string name, bool announce) {
				Name = name;
				Announce = announce;
			}
		}

		private static readonly Dictionary<ScreenId, Entry> Entries = new Dictionary<ScreenId, Entry> {
			{ ScreenId.Unknown,       new Entry("",                        false) },
			{ ScreenId.Loading,       new Entry(Strings.ScreenLoading,      false) },
			{ ScreenId.Intro,         new Entry(Strings.ScreenIntro,        false) },
			{ ScreenId.Attract,       new Entry(Strings.ScreenAttract,      false) },
			{ ScreenId.MainMenu,      new Entry(Strings.ScreenMainMenu,     true)  },
			{ ScreenId.DeckBuilder,   new Entry(Strings.ScreenDeckBuilder,  true)  },
			{ ScreenId.DungeonSelect, new Entry(Strings.ScreenDungeonSelect,true)  },
			{ ScreenId.Map,           new Entry(Strings.ScreenMap,          true)  },
			{ ScreenId.CardTable,     new Entry(Strings.ScreenCardTable,    true)  },
			{ ScreenId.Encounter,     new Entry(Strings.ScreenEncounter,    true)  },
			{ ScreenId.Combat,        new Entry(Strings.ScreenCombat,       true)  },
			{ ScreenId.Shop,          new Entry(Strings.ScreenShop,         true)  },
			// A modal dialogue announces its own prompt text dynamically, not this
			// generic name, so the auto-announce flag is off; the name is only the
			// fallback when the prompt can't be read.
			{ ScreenId.Dialogue,      new Entry(Strings.ScreenDialogue,     false) },
			{ ScreenId.Inventory,     new Entry(Strings.ScreenInventory,    true)  },
			{ ScreenId.Paused,        new Entry(Strings.ScreenPaused,       true)  },
			{ ScreenId.Results,       new Entry(Strings.ScreenResults,      true)  },
			{ ScreenId.Cabinet,       new Entry(Strings.ScreenCabinet,      true)  },
		};

		/// <summary>The spoken name for a screen.</summary>
		public static string NameOf(ScreenId id) => Entries[id].Name;

		/// <summary>Whether entering this screen should be spoken.</summary>
		public static bool ShouldAnnounce(ScreenId id) => Entries[id].Announce;

		/// <summary>
		/// The entry announcement: the name when the screen is announceable, else
		/// null. Single source for the "what to speak on entry" decision so the
		/// stack and any caller agree.
		/// </summary>
		public static string AnnouncementFor(ScreenId id) {
			Entry entry = Entries[id];
			return entry.Announce ? entry.Name : null;
		}

		/// <summary>All ids the catalog knows about (drives the coverage test).</summary>
		public static IEnumerable<ScreenId> AllIds => Entries.Keys;
	}
}
