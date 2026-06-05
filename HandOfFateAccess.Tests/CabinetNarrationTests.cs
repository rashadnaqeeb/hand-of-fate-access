using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The cabinet examine panel's readout: section title first, then the section's
	/// body labels, markup stripped, empties dropped, duplicates collapsed. Covers the
	/// display-only surface the focus model never reaches when examining a court card.
	/// </summary>
	public class CabinetNarrationTests {
		[Fact]
		public void Section_then_body_labels() {
			Assert.Equal(
				"Lore, A wanderer's tale.",
				CabinetNarration.Compose("Lore", new[] { "A wanderer's tale." }));
		}

		[Fact]
		public void Multiple_body_labels_joined_in_order() {
			Assert.Equal(
				"Deck Changes, 2 x Goblin, 1 x Curse of Thorns",
				CabinetNarration.Compose("Deck Changes", new[] { "2 x Goblin", "1 x Curse of Thorns" }));
		}

		[Fact]
		public void Markup_stripped_and_empties_dropped() {
			Assert.Equal(
				"Lore, Real text",
				CabinetNarration.Compose("Lore", new[] { "[b][/b]", "", "Real text" }));
		}

		[Fact]
		public void Body_only_when_section_empty() {
			Assert.Equal("Body", CabinetNarration.Compose(null, new[] { "Body" }));
			Assert.Equal("Body", CabinetNarration.Compose("", new[] { "Body" }));
		}

		[Fact]
		public void Empty_when_nothing_present() {
			Assert.Equal("", CabinetNarration.Compose(null, null));
			Assert.Equal("", CabinetNarration.Compose("", new string[0]));
		}

		[Fact]
		public void Duplicate_labels_collapsed() {
			Assert.Equal(
				"Lore, Tale",
				CabinetNarration.Compose("Lore", new[] { "Tale", "Tale" }));
		}
	}
}
