using UnityEngine;

namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Thin Unity adapter: extracts raw state from a focused GameObject into a
	/// plain Core DTO and does NO formatting. The only "cache" is the live
	/// components read at this moment; nothing is stored. Word choice lives in
	/// FocusComposer (Core), so this stays trivial and game-only.
	/// </summary>
	internal static class FocusAdapter {
		public static FocusDto Extract(GameObject go) {
			// Active labels only: hidden labels (tooltips, off-state copies) aren't
			// on screen, so they shouldn't be spoken. Hierarchy order is reading order.
			UILabel[] labels = go.GetComponentsInChildren<UILabel>();
			var texts = new string[labels.Length];
			for (int i = 0; i < labels.Length; i++)
				texts[i] = labels[i].text;

			return new FocusDto(go.name, texts);
		}
	}
}
