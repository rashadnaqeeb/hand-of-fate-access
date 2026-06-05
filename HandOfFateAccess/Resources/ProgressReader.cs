using HandOfFateAccess.Resources;
using UnityEngine;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// Extracts dungeon progress (run score and current level) into a Unity-free
	/// snapshot. This is a separate data source from the player resources: it lives on
	/// the dungeon and exists only in endless mode. The story dungeons return a null
	/// score, which leaves HasScore false so nothing is read, matching the sighted view
	/// (those runs show no score-and-level HUD). Values are read live; nothing is cached.
	/// Feeds both the live level-change announcement and the future stats key.
	/// </summary>
	internal static class ProgressReader {
		public static ProgressSnapshot Read() {
			var s = new ProgressSnapshot();
			DungeonManager manager = DungeonManager.Instance;
			Dungeon dungeon = manager != null ? manager.Dungeon : null;
			if (dungeon == null) return s;
			DungeonScore score = dungeon.Score;
			if (score == null) return s;

			s.HasScore = true;
			s.Score = score.TotalScore;
			s.Level = dungeon.LevelIndex + 1;
			s.LevelWord = UIUtils.GetString("SCORE_LEVEL");
			return s;
		}
	}
}
