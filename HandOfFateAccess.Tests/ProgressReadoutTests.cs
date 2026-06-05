using HandOfFateAccess.Localization;
using HandOfFateAccess.Resources;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// Dungeon-progress readout: the lone level line spoken on change, and the full
	/// score-and-level line for the stats key, which is empty outside endless mode.
	/// </summary>
	public class ProgressReadoutTests {
		[Fact]
		public void Level_line_uses_game_word_and_number() {
			Assert.Equal("Level 5", ProgressReadout.Level("Level", 5));
		}

		[Fact]
		public void Level_line_falls_back_to_number_when_word_missing() {
			Assert.Equal("5", ProgressReadout.Level("", 5));
			Assert.Equal("5", ProgressReadout.Level(null, 5));
		}

		[Fact]
		public void Compose_reads_score_then_level() {
			var s = new ProgressSnapshot { HasScore = true, Score = 1200, Level = 5, LevelWord = "Level" };
			Assert.Equal("1200 " + Strings.ProgressScore + ", Level 5", ProgressReadout.Compose(s));
		}

		[Fact]
		public void Compose_empty_without_score_hud() {
			// Story mode / no run: no score HUD exists, so nothing is read.
			Assert.Equal("", ProgressReadout.Compose(new ProgressSnapshot { HasScore = false, Score = 0, Level = 2 }));
			Assert.Equal("", ProgressReadout.Compose(null));
		}
	}
}
