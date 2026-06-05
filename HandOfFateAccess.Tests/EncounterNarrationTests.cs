using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class EncounterNarrationTests {
		[Fact]
		public void NarrativeThenInstructions_WhenBothPresent() {
			Assert.Equal(
				"The blade strikes true., Draw 1 Equipment Card.",
				EncounterNarration.Compose("The blade strikes true.", "Draw 1 Equipment Card."));
		}

		[Fact]
		public void NarrativeOnly_WhenInstructionsEmpty() {
			Assert.Equal("Story.", EncounterNarration.Compose("Story.", null));
			Assert.Equal("Story.", EncounterNarration.Compose("Story.", ""));
		}

		[Fact]
		public void InstructionsOnly_WhenNarrativeEmpty() {
			Assert.Equal("Draw 1 Card.", EncounterNarration.Compose(null, "Draw 1 Card."));
		}

		[Fact]
		public void Empty_WhenNeitherPresent() {
			Assert.Equal("", EncounterNarration.Compose(null, null));
			Assert.Equal("", EncounterNarration.Compose("", ""));
		}
	}
}
