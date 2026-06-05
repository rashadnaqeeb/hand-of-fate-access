using UnityEngine;

namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Tracks whether the current focus change was driven by the user pressing a key.
	/// User input (navigation, cancel, confirm) is dispatched through UISelectable.OnKey
	/// and DoClick, and any resulting selection change is recorded synchronously within
	/// that call, on the same frame. So a focus recorded on the same frame an input was
	/// dispatched is user-driven; one recorded on any other frame is the game landing
	/// focus on its own.
	///
	/// Frame-stamped rather than a held flag so an exception in the game's input handler
	/// can never strand the state "on" - there is nothing to reset.
	/// </summary>
	internal static class NavigationState {
		private static int _inputFrame = -1;

		/// <summary>Record that user input is being dispatched this frame.</summary>
		public static void Mark() => _inputFrame = Time.frameCount;

		/// <summary>Whether user input was dispatched on the current frame.</summary>
		public static bool IsActive => Time.frameCount == _inputFrame;
	}
}
