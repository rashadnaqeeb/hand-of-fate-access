using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The end-of-run scoreboard readout: header then one line per row, rows joined
	/// without cross-row de-duplication so repeated scores are not lost.
	/// </summary>
	public class ScoreboardNarrationTests {
		[Fact]
		public void Header_then_total_and_entries() {
			// Total row first (bare number), then bonus and multiplier entries.
			Assert.Equal(
				"Your Score. Knight of Dust, 1450. Encounters, plus 1200. Difficulty, times 2",
				ScoreboardNarration.Compose("Your Score", new[] {
					new[] { "Knight of Dust", "1450" },
					new[] { "Encounters", "+1200" },
					new[] { "Difficulty", "x2" },
				}));
		}

		[Fact]
		public void Bonus_symbol_spoken_as_word() {
			Assert.Equal("Gold, plus 50", ScoreboardNarration.Compose(null, new[] {
				new[] { "Gold", "+50" },
			}));
		}

		[Fact]
		public void Multiplier_symbol_spoken_as_word() {
			Assert.Equal("Fame, times 3", ScoreboardNarration.Compose(null, new[] {
				new[] { "Fame", "x3" },
			}));
		}

		[Fact]
		public void Repeated_scores_across_rows_kept() {
			// Two rows share the same points; both must survive (no cross-row dedup).
			Assert.Equal(
				"Gold, plus 0. Food, plus 0",
				ScoreboardNarration.Compose(null, new[] {
					new[] { "Gold", "+0" },
					new[] { "Food", "+0" },
				}));
		}

		[Fact]
		public void Markup_stripped_and_empty_points_dropped() {
			Assert.Equal(
				"Score. Bandits",
				ScoreboardNarration.Compose("[b]Score[/b]", new[] {
					new[] { "Bandits", "" },
				}));
		}

		[Fact]
		public void Empty_when_no_header_and_no_rows() {
			Assert.Equal("", ScoreboardNarration.Compose(null, null));
			Assert.Equal("", ScoreboardNarration.Compose("", new string[0][]));
		}
	}
}
