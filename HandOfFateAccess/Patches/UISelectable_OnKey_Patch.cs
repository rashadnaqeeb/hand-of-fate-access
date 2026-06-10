using HandOfFateAccess.Focus;
using HandOfFateAccess.Glossary;
using HandOfFateAccess.Maps;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.OnKey, the dispatch point for user navigation and cancel
	/// keys (arrows, stick, Escape, Tab). Two jobs:
	///
	/// It swallows a key that one of the mod's own overlays owns (returns false to skip
	/// the original), because the game's selection must not also move with it: a
	/// Ctrl+arrow on the map drives the free-roam cursor, and any arrow while the sound
	/// glossary is open drives its list. Everything else falls through, so the game's
	/// nav still works.
	///
	/// Otherwise it marks the frame as user-driven so the selection change that follows
	/// synchronously within this call is announced with interrupt rather than queued. Reads
	/// no game state.
	/// </summary>
	internal static class UISelectable_OnKey_Patch {
		private static bool Prefix(KeyCode a_keycode) {
			if (GlossaryState.ShouldConsumeKey(a_keycode))
				return false;
			if (MapInput.ShouldConsumeArrow(a_keycode))
				return false;
			NavigationState.Mark();
			return true;
		}
	}
}
