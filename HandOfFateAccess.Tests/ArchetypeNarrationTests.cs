using HandOfFateAccess.Localization;
using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The Fates pile's two spoken lines: the archetype name (with "locked" when it is not
	/// unlocked) and the active info section (title then body labels). Both run through the
	/// Message pipeline, so markup is stripped, empties dropped, and duplicates collapsed.
	/// </summary>
	public class ArchetypeNarrationTests {
		[Fact]
		public void Name_alone_when_unlocked() {
			Assert.Equal("Knight", ArchetypeNarration.ComposeName("Knight", locked: false));
		}

		[Fact]
		public void Locked_appended_when_not_unlocked() {
			Assert.Equal("Knight, " + Strings.CardLocked, ArchetypeNarration.ComposeName("Knight", locked: true));
		}

		[Fact]
		public void Empty_name_composes_empty() {
			Assert.Equal("", ArchetypeNarration.ComposeName(null, locked: false));
			Assert.Equal("", ArchetypeNarration.ComposeName("", locked: true));
		}

		[Fact]
		public void Section_then_body_labels() {
			Assert.Equal(
				"Loadout, 30 health, 1 x Sword",
				ArchetypeNarration.ComposeSection("Loadout", new[] { "30 health", "1 x Sword" }));
		}

		[Fact]
		public void Section_markup_stripped_and_empties_dropped() {
			Assert.Equal(
				"Description, Real text",
				ArchetypeNarration.ComposeSection("Description", new[] { "[b][/b]", "", "Real text" }));
		}

		[Fact]
		public void Section_empty_when_nothing_present() {
			Assert.Equal("", ArchetypeNarration.ComposeSection(null, null));
			Assert.Equal("", ArchetypeNarration.ComposeSection("", new string[0]));
		}
	}
}
