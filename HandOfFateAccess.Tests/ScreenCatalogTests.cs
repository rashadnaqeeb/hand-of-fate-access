using System;
using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The catalog is the only place screen wording lives; every id must resolve to
	/// a name, and the announce flags decide what the player hears on entry.
	/// </summary>
	public class ScreenCatalogTests {
		[Fact]
		public void Every_screen_id_has_a_catalog_entry() {
			foreach (ScreenId id in Enum.GetValues(typeof(ScreenId))) {
				// Throws (KeyNotFoundException) if an id is missing an entry.
				ScreenCatalog.NameOf(id);
				ScreenCatalog.ShouldAnnounce(id);
			}
		}

		[Fact]
		public void Announceable_screens_have_a_nonempty_name() {
			foreach (ScreenId id in Enum.GetValues(typeof(ScreenId)))
				if (ScreenCatalog.ShouldAnnounce(id))
					Assert.False(string.IsNullOrEmpty(ScreenCatalog.NameOf(id)), id.ToString());
		}

		[Fact]
		public void Transient_states_do_not_announce() {
			Assert.False(ScreenCatalog.ShouldAnnounce(ScreenId.Unknown));
			Assert.False(ScreenCatalog.ShouldAnnounce(ScreenId.Loading));
			Assert.False(ScreenCatalog.ShouldAnnounce(ScreenId.Intro));
			Assert.False(ScreenCatalog.ShouldAnnounce(ScreenId.Attract));
		}

		[Fact]
		public void Context_screens_announce() {
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.MainMenu));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.Map));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.CardTable));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.Encounter));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.Combat));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.Shop));
			Assert.True(ScreenCatalog.ShouldAnnounce(ScreenId.Paused));
		}

		[Fact]
		public void AnnouncementFor_returns_name_when_announceable_else_null() {
			Assert.Equal("Main menu", ScreenCatalog.AnnouncementFor(ScreenId.MainMenu));
			Assert.Null(ScreenCatalog.AnnouncementFor(ScreenId.Loading));
		}

		[Fact]
		public void Names_are_mod_authored_clean_wording() {
			Assert.Equal("Card table", ScreenCatalog.NameOf(ScreenId.CardTable));
			Assert.Equal("Deck builder", ScreenCatalog.NameOf(ScreenId.DeckBuilder));
			Assert.Equal("Dungeon select", ScreenCatalog.NameOf(ScreenId.DungeonSelect));
		}
	}
}
