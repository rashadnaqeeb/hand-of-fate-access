using UnityEngine;

namespace HandOfFateAccess.Maps {
	/// <summary>
	/// Shared state between the map cursor's input and the OnKey patch. The plugin sets
	/// <see cref="OnMap"/> each frame from the screen stack; both the directional binding
	/// (which moves the cursor) and the OnKey patch (which must swallow the arrow so the
	/// game's own selection does not also move) read it, so the two agree on exactly when
	/// the cursor owns Ctrl+arrows.
	/// </summary>
	internal static class MapInput {
		/// <summary>The map is the active screen (no encounter/shop/etc. overlay on top).</summary>
		public static bool OnMap;

		/// <summary>
		/// Whether the game's nav should be skipped for this key: a Ctrl+arrow on the map,
		/// which the cursor handles instead. Plain arrows (no Ctrl) fall through to the game.
		/// </summary>
		public static bool ShouldConsumeArrow(KeyCode key) {
			return OnMap && IsArrow(key) && CtrlHeld();
		}

		private static bool IsArrow(KeyCode key) {
			return key == KeyCode.UpArrow || key == KeyCode.DownArrow
				|| key == KeyCode.LeftArrow || key == KeyCode.RightArrow;
		}

		private static bool CtrlHeld() {
			return UnityEngine.Input.GetKey(KeyCode.LeftControl)
				|| UnityEngine.Input.GetKey(KeyCode.RightControl);
		}
	}
}
