using HandOfFateAccess.Resources;
using HandOfFateAccess.Speech;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// Announces the dungeon level as it changes ("Level 5"), so the player learns how
	/// deep they are as they advance. Endless mode only: the level is read off the same
	/// score data the sighted HUD shows, which the story dungeons do not have, so a story
	/// run reports no level and stays silent (matching the sighted view). The level is
	/// edge-detected from the update loop rather than via a per-run event subscription;
	/// when no score HUD is live the marker resets, so the next endless run re-announces
	/// from level one. Queued so it reads after the board's screen-change announcement.
	/// </summary>
	internal sealed class ProgressWatcher {
		private int _lastLevel = -1;

		public void Pump() {
			ProgressSnapshot s = ProgressReader.Read();
			if (!s.HasScore) {
				_lastLevel = -1;
				return;
			}
			if (s.Level == _lastLevel) return;
			_lastLevel = s.Level;
			string line = ProgressReadout.Level(s.LevelWord, s.Level);
			if (!string.IsNullOrEmpty(line))
				SpeechPipeline.SpeakQueued(line);
		}
	}
}
