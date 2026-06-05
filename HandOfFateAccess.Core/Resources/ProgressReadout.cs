using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// A plain snapshot of dungeon progress: the run score and current level. This is a
	/// separate data source from the player resources (it lives on the dungeon, not the
	/// player) and only exists in endless mode, where the game shows a score-and-level
	/// HUD. In the story dungeons that HUD is hidden (no dungeon score), so HasScore is
	/// false and nothing is read, matching the sighted view. LevelWord is the game's own
	/// localized "Level" word, supplied by the adapter; Level is 1-based.
	/// </summary>
	public sealed class ProgressSnapshot {
		public bool HasScore;
		public int Score;
		public int Level;
		public string LevelWord;
	}

	/// <summary>
	/// Composes dungeon progress for speech. <see cref="Level"/> is the lone level line
	/// ("Level 5"), spoken on its own when the level changes. <see cref="Compose"/> is the
	/// full score-and-level line for the on-demand stats key, meant to be read after the
	/// resources. Both return "" when there is no score HUD (story mode / no run), so the
	/// stats key simply adds nothing for those runs.
	/// </summary>
	public static class ProgressReadout {
		public static string Level(string levelWord, int level) {
			if (string.IsNullOrEmpty(levelWord)) return level.ToString();
			return levelWord + " " + level;
		}

		public static string Compose(ProgressSnapshot s) {
			if (s == null || !s.HasScore) return "";
			return new Message()
				.Add(s.Score + " " + Strings.ProgressScore)
				.Add(Level(s.LevelWord, s.Level))
				.Resolve();
		}
	}
}
