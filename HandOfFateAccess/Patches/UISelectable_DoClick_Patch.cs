using HandOfFateAccess.Focus;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Prefix on UISelectable.DoClick, the dispatch point for the user confirming a
	/// control (submit button / click). Marks the frame as user-driven so a focus the
	/// game lands as a direct consequence (within this call) is treated as user-driven.
	/// When that consequence is a new screen or dialogue, the announce policy still
	/// queues the focus behind the freshly announced context. Never skips the original.
	/// </summary>
	internal static class UISelectable_DoClick_Patch {
		private static void Prefix() => NavigationState.Mark();
	}
}
