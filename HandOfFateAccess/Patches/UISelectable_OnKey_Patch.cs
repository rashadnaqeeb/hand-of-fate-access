using HandOfFateAccess.Focus;
using HandOfFateAccess.Maps;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.OnKey, the dispatch point for user navigation and cancel
	/// keys (arrows, stick, Escape, Tab). Two jobs:
	///
	/// It swallows a Ctrl+arrow on the map (returns false to skip the original), because
	/// that key drives the mod's free-roam cursor and the game's own selection must not move
	/// with it. Plain arrows fall through, so the game's nav still works.
	///
	/// Otherwise it marks the frame as user-driven so the selection change that follows
	/// synchronously within this call is announced with interrupt rather than queued. Reads
	/// no game state.
	/// </summary>
	internal static class UISelectable_OnKey_Patch {
		private static bool Prefix(KeyCode a_keycode) {
			if (MapInput.ShouldConsumeArrow(a_keycode))
				return false;
			NavigationState.Mark();
			return true;
		}
	}
}
