using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// One item in the equipment-replace comparison, as raw label text pulled from a
	/// display-only InfoPanel. The stat is split the way the panel splits it (a title like
	/// "Damage" and a value like "5"); composition rejoins them.
	/// </summary>
	public sealed class CompareItem {
		public string Title;
		public string StatTitle;
		public string StatValue;
		public string Description;
	}

	/// <summary>
	/// Composes the equipment-replace prompt: "Equip {new}, replacing {old}", where each
	/// item reads as name, stat, then rules text. The new-vs-old comparison lives on
	/// display-only panels the focus model never reaches, and the prompt's confirm/cancel
	/// has no navigable buttons, so without this the decision is silent. The action hint
	/// (Strings.EquipReplaceHint) is spoken separately by the caller, after this line.
	/// Returns "" when there is nothing to compare.
	/// </summary>
	public static class CompareNarration {
		public static string Compose(CompareItem newItem, CompareItem oldItem) {
			string newLine = Line(newItem);
			string oldLine = Line(oldItem);
			if (newLine.Length == 0 || oldLine.Length == 0) return "";
			return Strings.EquipReplace(newLine, oldLine);
		}

		private static string Line(CompareItem item) {
			if (item == null) return "";
			return new Message()
				.Add(item.Title)
				.Add(Stat(item.StatTitle, item.StatValue))
				.Add(item.Description)
				.Resolve();
		}

		// The panel shows the stat as a separate label and value (e.g. "Damage" and "5");
		// rejoin them into one phrase so they read together rather than as two list items.
		private static string Stat(string title, string value) {
			string t = (title ?? "").Trim();
			string v = (value ?? "").Trim();
			if (t.Length == 0) return v;
			if (v.Length == 0) return t;
			return t + " " + v;
		}
	}
}
