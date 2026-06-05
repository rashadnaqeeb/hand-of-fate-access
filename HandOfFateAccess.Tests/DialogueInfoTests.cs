using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// A modal dialogue speaks its prompt body; options are only a fallback so the
	/// modal is never announced silently. Buttons are read by focus, so they are not
	/// folded into the body line.
	/// </summary>
	public class DialogueInfoTests {
		[Fact]
		public void Body_is_the_spoken_line() {
			var info = new DialogueInfo("Are you sure you want to forfeit?", new[] { "Yes", "No" });
			Assert.Equal("Are you sure you want to forfeit?", info.Compose());
		}

		[Fact]
		public void Body_markup_is_stripped() {
			var info = new DialogueInfo("[b]Quit[/b] the game?", null);
			Assert.Equal("Quit the game?", info.Compose());
		}

		[Fact]
		public void Options_are_not_appended_when_body_is_present() {
			var info = new DialogueInfo("Confirm?", new[] { "OK" });
			Assert.Equal("Confirm?", info.Compose());
		}

		[Fact]
		public void Options_are_the_fallback_when_body_is_empty() {
			var info = new DialogueInfo("", new[] { "OK" });
			Assert.Equal("OK", info.Compose());
		}

		[Fact]
		public void Generic_name_is_the_last_resort() {
			var info = new DialogueInfo(null, null);
			Assert.Equal("Dialogue", info.Compose());
		}

		[Fact]
		public void Whitespace_body_falls_through_to_options() {
			var info = new DialogueInfo("   ", new[] { "Continue" });
			Assert.Equal("Continue", info.Compose());
		}
	}
}
