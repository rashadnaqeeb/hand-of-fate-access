using HandOfFateAccess.Gambit;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ChanceOutcomeKeysTests {
		[Theory]
		[InlineData(ChanceOutcome.Success, "CHANCE_TITLE_SUCCESS")]
		[InlineData(ChanceOutcome.HugeSuccess, "CHANCE_TITLE_HUGE_SUCCESS")]
		[InlineData(ChanceOutcome.Failure, "CHANCE_TITLE_FAILURE")]
		[InlineData(ChanceOutcome.HugeFailure, "CHANCE_TITLE_HUGE_FAILURE")]
		public void Title_MapsEachOutcomeToItsGameKey(ChanceOutcome outcome, string expected) {
			Assert.Equal(expected, ChanceOutcomeKeys.Title(outcome));
		}

		// An out-of-range value returns a key absent from every locale, so Localize leaves it
		// unspoken rather than naming a wrong outcome.
		[Fact]
		public void Title_ReturnsAbsentKey_ForUndefinedOutcome() {
			Assert.Equal("CHANCE_TITLE_UNMAPPED", ChanceOutcomeKeys.Title((ChanceOutcome)999));
		}
	}
}
