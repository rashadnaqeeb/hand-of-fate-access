using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The end-of-run scoreboard readout: header then one line per row, rows joined
	/// without cross-row de-duplication so repeated scores are not lost.
	/// </summary>
	public class ScoreboardNarrationTests {
		[Fact]
		public void Header_then_rows() {
			Assert.Equal(
				"Your Score. Encounters, 1200. Monsters Slain, 800",
				ScoreboardNarration.Compose("Your Score", new[] {
					new[] { "Encounters", "1200" },
					new[] { "Monsters Slain", "800" },
				}));
		}

		[Fact]
		public void Repeated_scores_across_rows_kept() {
			// Two rows share a score of 0; both must survive (no cross-row dedup).
			Assert.Equal(
				"Gold, 0. Food, 0",
				ScoreboardNarration.Compose(null, new[] {
					new[] { "Gold", "0" },
					new[] { "Food", "0" },
				}));
		}

		[Fact]
		public void Subscore_included_when_present() {
			Assert.Equal(
				"Total. Fame, 50, x2",
				ScoreboardNarration.Compose("Total", new[] {
					new[] { "Fame", "50", "x2" },
				}));
		}

		[Fact]
		public void Markup_stripped_and_empty_fields_dropped() {
			Assert.Equal(
				"Score. Bandits, 5",
				ScoreboardNarration.Compose("[b]Score[/b]", new[] {
					new[] { "Bandits", "", "5" },
				}));
		}

		[Fact]
		public void Empty_when_no_header_and_no_rows() {
			Assert.Equal("", ScoreboardNarration.Compose(null, null));
			Assert.Equal("", ScoreboardNarration.Compose("", new string[0][]));
		}
	}
}
