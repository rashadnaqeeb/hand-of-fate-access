using HandOfFateAccess.Focus;
using HandOfFateAccess.Glossary;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.DoClick, the dispatch point for the user confirming a
	/// control (submit button / click). While the sound glossary overlay is open,
	/// confirm belongs to it (it replays the current entry's sound), so the focused
	/// pause-menu control underneath is skipped. Otherwise it marks the frame as
	/// user-driven so a focus the game lands as a direct consequence (within this
	/// call) is treated as user-driven. When that consequence is a new screen or
	/// dialogue, the announce policy still queues the focus behind the freshly
	/// announced context.
	/// </summary>
	internal static class UISelectable_DoClick_Patch {
		private static bool Prefix() {
			if (GlossaryState.Open) return false;
			NavigationState.Mark();
			return true;
		}
	}
}
