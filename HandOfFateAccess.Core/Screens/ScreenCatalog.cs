using System.Collections.Generic;

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
			{ ScreenId.Unknown,       new Entry("",               false) },
			{ ScreenId.Loading,       new Entry("Loading",        false) },
			{ ScreenId.Intro,         new Entry("Intro",          false) },
			{ ScreenId.Attract,       new Entry("Attract",        false) },
			{ ScreenId.MainMenu,      new Entry("Main menu",      true)  },
			{ ScreenId.DeckBuilder,   new Entry("Deck builder",   true)  },
			{ ScreenId.DungeonSelect, new Entry("Dungeon select", true)  },
			{ ScreenId.Map,           new Entry("Map",            true)  },
			{ ScreenId.CardTable,     new Entry("Card table",     true)  },
			{ ScreenId.Encounter,     new Entry("Encounter",      true)  },
			{ ScreenId.Combat,        new Entry("Combat",         true)  },
			{ ScreenId.Shop,          new Entry("Shop",           true)  },
			// A modal dialogue announces its own prompt text dynamically, not this
			// generic name, so the auto-announce flag is off; the name is only the
			// fallback when the prompt can't be read.
			{ ScreenId.Dialogue,      new Entry("Dialogue",       false) },
			{ ScreenId.Inventory,     new Entry("Inventory",      true)  },
			{ ScreenId.Paused,        new Entry("Paused",         true)  },
			{ ScreenId.Results,       new Entry("Results",        true)  },
			{ ScreenId.Cabinet,       new Entry("Cabinet",        true)  },
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
