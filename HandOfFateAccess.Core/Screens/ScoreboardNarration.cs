using System.Collections.Generic;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the end-of-run scoreboard from its display-only labels: the header line,
	/// then one line per score row (title, score, optional sub-score). Each row is built
	/// through its own Message so markup is stripped, empties dropped, and its fields
	/// joined with the usual comma; rows are joined with a period so the reader gets a
	/// clear break between score lines. Rows are NOT de-duplicated against each other:
	/// two rows that happen to share a score (e.g. both zero) must both be spoken, or the
	/// breakdown would silently lose a row. Returns "" when there is nothing to read.
	/// </summary>
	public static class ScoreboardNarration {
		public static string Compose(string header, IList<string[]> rows) {
			var lines = new List<string>();
			string h = new Message().Add(header).Resolve();
			if (h.Length > 0)
				lines.Add(h);
			if (rows != null) {
				foreach (string[] row in rows) {
					string line = new Message().AddRange(row).Resolve();
					if (line.Length > 0)
						lines.Add(line);
				}
			}
			return string.Join(". ", lines.ToArray());
		}
	}
}
