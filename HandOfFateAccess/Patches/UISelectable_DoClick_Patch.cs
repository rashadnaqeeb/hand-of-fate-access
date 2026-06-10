using HandOfFateAccess.Focus;
using HandOfFateAccess.Glossary;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.DoClick, the dispatch point for the user confirming a
	/// control. UICamera funnels Enter, controller A and mouse click into it, so while
	/// the sound glossary overlay is open a confirm from any device is recorded for its
	/// pump (it replays the current entry's sound) and the control underneath is
	/// skipped. Otherwise it marks the frame as user-driven so a focus the game lands
	/// as a direct consequence (within this call) is treated as user-driven. When that
	/// consequence is a new screen or dialogue, the announce policy still queues the
	/// focus behind the freshly announced context.
	/// </summary>
	internal static class UISelectable_DoClick_Patch {
		private static bool Prefix() {
			if (GlossaryState.CapturePlay()) return false;
			NavigationState.Mark();
			return true;
		}
	}
}
