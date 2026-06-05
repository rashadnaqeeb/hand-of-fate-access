using HandOfFateAccess.Focus;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.OnKey, the dispatch point for user navigation and cancel
	/// keys (arrows, stick, Escape, Tab). Marks the frame as user-driven so the
	/// selection change that follows synchronously within this call is announced with
	/// interrupt rather than queued. Reads no game state and never skips the original.
	/// </summary>
	internal static class UISelectable_OnKey_Patch {
		private static void Prefix() => NavigationState.Mark();
	}
}
