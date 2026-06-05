using System.Collections.Generic;
using HandOfFateAccess.Speech;

namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Turns a FocusDto into the line spoken when focus lands on a control.
	/// This is the generic Phase 2 readout: every label's text, markup stripped,
	/// empties dropped, duplicates (NGUI shadow/outline copies) collapsed. When a
	/// control carries no label text, its object name is the only thing to say.
	/// Targeted per-control readouts come later (Phase 3 proxies).
	/// </summary>
	public static class FocusComposer {
		public static string Compose(FocusDto focus) {
			if (focus == null) return "";

			var parts = new List<string>();
			var seen = new HashSet<string>();
			foreach (string raw in focus.Labels) {
				string filtered = TextFilter.FilterForSpeech(raw);
				if (string.IsNullOrEmpty(filtered)) continue;
				if (!seen.Add(filtered)) continue;
				parts.Add(filtered);
			}

			if (parts.Count > 0)
				return string.Join(", ", parts.ToArray());

			// No usable label text; the object name is all we have to identify it.
			return TextFilter.FilterForSpeech(focus.Name);
		}
	}
}
