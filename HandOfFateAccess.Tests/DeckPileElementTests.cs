using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// A focused deck-builder pile: title, then for the limited piles the card count
	/// toward its limit and a status word when off that limit. Mirrors the game's
	/// DeckInfoPanel (title, "N/M" counter, insufficient / too-many icon). Covers the
	/// silent-failure surface where the pile would otherwise speak its top card's title.
	/// </summary>
	public class DeckPileElementTests {
		private static string Describe(DeckPileInfo info) =>
			new DeckPileElement(info).Describe().Resolve();

		[Fact]
		public void Archetype_pile_reads_title_only() {
			Assert.Equal("Fates", Describe(new DeckPileInfo("Fates")));
		}

		[Fact]
		public void Pile_on_limit_reads_count_without_status() {
			Assert.Equal("Encounter, 12/12", Describe(new DeckPileInfo("Encounter", 12, 12)));
		}

		[Fact]
		public void Pile_under_limit_reads_insufficient() {
			Assert.Equal("Equipment, 8/12, " + Strings.DeckInsufficient,
				Describe(new DeckPileInfo("Equipment", 8, 12)));
		}

		[Fact]
		public void Pile_over_limit_reads_too_many() {
			Assert.Equal("Equipment, 15/12, " + Strings.DeckTooMany,
				Describe(new DeckPileInfo("Equipment", 15, 12)));
		}

		[Fact]
		public void Empty_pile_reads_zero_of_limit_insufficient() {
			Assert.Equal("Encounter, 0/15, " + Strings.DeckInsufficient,
				Describe(new DeckPileInfo("Encounter", 0, 15)));
		}
	}
}
