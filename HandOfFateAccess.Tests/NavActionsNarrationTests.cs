using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The nav bar context-action readout: the shown button labels, markup stripped,
	/// empties dropped, comma-joined; null or none yields "".
	/// </summary>
	public class NavActionsNarrationTests {
		[Fact]
		public void Joins_the_action_labels() {
			Assert.Equal(
				"View previous high score, Restart",
				NavActionsNarration.Compose(new[] { "View previous high score", "Restart" }));
		}

		[Fact]
		public void Strips_markup_and_drops_empties() {
			Assert.Equal(
				"Fill deck",
				NavActionsNarration.Compose(new[] { "[b]Fill deck[/b]", "" }));
		}

		[Fact]
		public void Null_or_empty_yields_empty() {
			Assert.Equal("", NavActionsNarration.Compose(null));
			Assert.Equal("", NavActionsNarration.Compose(new string[0]));
		}
	}
}
