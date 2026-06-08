using System.Collections.Generic;
using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the end-of-run scoreboard from its display-only labels: the header line,
	/// then one line per score row (title, then the points). Each row is built through its
	/// own Message so markup is stripped, empties dropped, and its fields joined with the
	/// usual comma; rows are joined with a period so the reader gets a clear break between
	/// score lines. Rows are NOT de-duplicated against each other: two rows that happen to
	/// share a score (e.g. both zero) must both be spoken, or the breakdown would silently
	/// lose a row.
	///
	/// The points carry the game's symbol ("x2", "+50", or a bare total), which is spoken as
	/// a word so it reads as speech. The game's separate sub-score column is not read: it is
	/// the raw quantity behind the points (count or amount), which equals the points whenever
	/// their ratio is one and otherwise trails as an unlabelled number, so it muddies the line
	/// more than it informs. Returns "" when there is nothing to read.
	/// </summary>
	public static class ScoreboardNarration {
		public static string Compose(string header, IList<string[]> rows) {
			var lines = new List<string>();
			string h = new Message().Add(header).Resolve();
			if (h.Length > 0)
				lines.Add(h);
			if (rows != null) {
				foreach (string[] row in rows) {
					string line = ComposeRow(row);
					if (line.Length > 0)
						lines.Add(line);
				}
			}
			return string.Join(". ", lines.ToArray());
		}

		// A row is [title, points]. The points is spoken with its symbol turned into a word.
		private static string ComposeRow(string[] row) {
			var message = new Message();
			if (row.Length > 0)
				message.Add(row[0]);
			if (row.Length > 1)
				message.Add(SpeakPoints(row[1]));
			return message.Resolve();
		}

		// The game prefixes a multiplier with "x" and a bonus with "+"; a total has no prefix.
		// Speak the prefix as a word and keep the number; a bare total reads unchanged.
		private static string SpeakPoints(string points) {
			if (string.IsNullOrEmpty(points))
				return points;
			switch (points[0]) {
				case 'x':
				case 'X': return Strings.ScoreTimes + " " + points.Substring(1);
				case '+': return Strings.ScorePlus + " " + points.Substring(1);
				default: return points;
			}
		}
	}
}
