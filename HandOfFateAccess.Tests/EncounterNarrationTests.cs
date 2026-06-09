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

		[Fact]
		public void Decide_SpeaksFullLine_WhenNarrativeAndInstructionsArriveTogether() {
			var r = EncounterNarration.Decide(true, "You open the chest.", "Draw 4 Equipment Cards.", null, null);
			Assert.True(r.Speak);
			Assert.Equal("You open the chest., Draw 4 Equipment Cards.", r.Text);
			Assert.Equal("You open the chest.", r.MarkerNarrative);
			Assert.Equal("Draw 4 Equipment Cards.", r.MarkerInstructions);
		}

		[Fact]
		public void Decide_StaysSilent_WhenNothingChanged() {
			var r = EncounterNarration.Decide(true, "You open the chest.", null, "You open the chest.", null);
			Assert.False(r.Speak);
			Assert.Equal("You open the chest.", r.MarkerNarrative);
		}

		// The bug: the result narrative shows alone, then the instructions are appended under
		// the unchanged narrative a frame later (after the player confirms). Only the new
		// instructions are spoken, not the whole line, so the narrative is not repeated.
		[Fact]
		public void Decide_SpeaksInstructionsAlone_WhenAppendedUnderUnchangedNarrative() {
			var first = EncounterNarration.Decide(true, "After a mighty heave, the spoils are yours!", "", null, null);
			Assert.True(first.Speak);
			Assert.Equal("After a mighty heave, the spoils are yours!", first.Text);

			var appended = EncounterNarration.Decide(true, "After a mighty heave, the spoils are yours!",
				"The dealer draws you 4 Gain Cards.", first.MarkerNarrative, first.MarkerInstructions);
			Assert.True(appended.Speak);
			Assert.Equal("The dealer draws you 4 Gain Cards.", appended.Text);
		}

		// The game blanks both labels for a sub-second tween mid-transition. The blank must
		// not re-arm the markers, or the line is announced again when it lands.
		[Fact]
		public void Decide_HoldsMarkers_ThroughTransientBlank() {
			var blank = EncounterNarration.Decide(true, "", "", "You open the chest.", null);
			Assert.False(blank.Speak);
			Assert.Equal("You open the chest.", blank.MarkerNarrative);

			var reappears = EncounterNarration.Decide(true, "You open the chest.", null,
				blank.MarkerNarrative, blank.MarkerInstructions);
			Assert.False(reappears.Speak);
		}

		// A genuine teardown re-arms (markers null) so the next encounter announces even if
		// its first beat repeats this one's last.
		[Fact]
		public void Decide_ReArms_WhenPanelNotShowing() {
			var r = EncounterNarration.Decide(false, null, null, "You open the chest.", null);
			Assert.False(r.Speak);
			Assert.Null(r.MarkerNarrative);
			Assert.Null(r.MarkerInstructions);

			var next = EncounterNarration.Decide(true, "You open the chest.", null,
				r.MarkerNarrative, r.MarkerInstructions);
			Assert.True(next.Speak);
		}
	}
}
