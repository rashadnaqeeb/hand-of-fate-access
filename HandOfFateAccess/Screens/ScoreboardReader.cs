using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the end-of-run scoreboard: the header line and each score row (title and the
	/// points). The rows are display-only; only the continue button is focusable, so the
	/// focus model never reaches the breakdown. The screen watcher edge-polls this on the
	/// results states and announces it. The Scoreboard instance is found in the live
	/// results-menu hierarchy; the rows are read in stored order (total first, then entries).
	/// The item's sub-score label is not read (see ScoreboardNarration for why). Private
	/// fields are reached by reflection so a game-side rename crashes the build's audit
	/// rather than degrading silently.
	/// </summary>
	internal static class ScoreboardReader {
		private static readonly FieldInfo TitleField = AccessTools.Field(typeof(Scoreboard), "m_title");
		private static readonly FieldInfo ItemsField = AccessTools.Field(typeof(Scoreboard), "m_items");
		private static readonly FieldInfo ItemTitleField = AccessTools.Field(typeof(ScoreboardItem), "m_title");
		private static readonly FieldInfo ItemScoreField = AccessTools.Field(typeof(ScoreboardItem), "m_score");

		/// <summary>
		/// The header and one row of label text per score entry, or nulls when no
		/// scoreboard is present.
		/// </summary>
		public static void Read(out string header, out List<string[]> rows) {
			header = null;
			rows = null;
			Scoreboard board = Object.FindObjectOfType<Scoreboard>();
			if (board == null) return;

			header = LabelText(TitleField, board);
			var items = (List<ScoreboardItem>)ItemsField.GetValue(board);
			rows = new List<string[]>();
			if (items == null) return;
			foreach (ScoreboardItem item in items) {
				if (item == null) continue;
				rows.Add(new[] {
					LabelText(ItemTitleField, item),
					LabelText(ItemScoreField, item),
				});
			}
		}

		private static string LabelText(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}
	}
}
