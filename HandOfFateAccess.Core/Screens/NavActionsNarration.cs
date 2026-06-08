using System.Collections.Generic;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the nav bar's context-action labels (the game's Function0/Function1 buttons,
	/// e.g. the scoreboard's high-score toggle and restart, the deck builder's fill-deck) into
	/// one spoken line. The game maps these to controller face buttons and function keys, and
	/// they sit outside the focusable selection, so the focus path never reaches them; the
	/// screen watcher edge-polls them and announces when they change. The labels are the game's
	/// own localized button text, so this only filters markup, drops empties, dedupes, and
	/// comma-joins. Returns "" when no such actions are shown.
	/// </summary>
	public static class NavActionsNarration {
		public static string Compose(IList<string> labels) {
			if (labels == null) return string.Empty;
			return new Message().AddRange(labels).Resolve();
		}
	}
}
