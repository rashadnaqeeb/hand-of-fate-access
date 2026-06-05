using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class EncounterNarrationTests {
		[Fact]
		public void NarrativeThenInstructions_WhenBothPresent() {
			Assert.Equal(
				"The blade strikes true., Draw 1 Equipment Card.",
				EncounterNarration.Compose("The blade strikes true.", "Draw 1 Equipment Card.", null));
		}

		[Fact]
		public void NarrativeOnly_WhenInstructionsEmpty() {
			Assert.Equal("Story.", EncounterNarration.Compose("Story.", null, null));
			Assert.Equal("Story.", EncounterNarration.Compose("Story.", "", null));
		}

		[Fact]
		public void InstructionsOnly_WhenNarrativeEmpty() {
			Assert.Equal("Draw 1 Card.", EncounterNarration.Compose(null, "Draw 1 Card.", null));
		}

		[Fact]
		public void Empty_WhenNeitherPresent() {
			Assert.Equal("", EncounterNarration.Compose(null, null, null));
			Assert.Equal("", EncounterNarration.Compose("", "", null));
		}

		[Fact]
		public void DropsNarrative_WhenItMatchesCardDescription() {
			// The opening scenario equals the card description already heard on focus.
			Assert.Equal("", EncounterNarration.Compose("Scenario text.", null, "Scenario text."));
		}

		[Fact]
		public void DropsScenarioButKeepsInstructions_WhenScenarioMatchesCard() {
			Assert.Equal(
				"This card's token is now yours.",
				EncounterNarration.Compose("Scenario text.", "This card's token is now yours.", "Scenario text."));
		}

		[Fact]
		public void KeepsNarrative_WhenItDiffersFromCardDescription() {
			// A choice result differs from the card description, so it is announced.
			Assert.Equal("Result text.", EncounterNarration.Compose("Result text.", null, "Scenario text."));
		}
	}
}
