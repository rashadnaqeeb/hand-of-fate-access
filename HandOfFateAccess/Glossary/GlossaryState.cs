using UnityEngine;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// Shared state between the sound glossary and the selection patches, mirroring
	/// <see cref="HandOfFateAccess.Maps.MapInput"/>: while the glossary overlay is open
	/// it owns the arrow keys (the pause menu's selection underneath must not move) and
	/// the confirm action (the focused pause control must not activate). The glossary
	/// sets <see cref="Open"/>; the OnKey and DoClick patches read it.
	/// </summary>
	internal static class GlossaryState {
		/// <summary>The glossary overlay is open over the pause menu.</summary>
		public static bool Open;

		/// <summary>Whether the game's nav should be skipped for this key: any arrow
		/// while the glossary is open. Escape and Tab fall through, so the game's
		/// cancel still closes the pause menu (the glossary closes with it).</summary>
		public static bool ShouldConsumeKey(KeyCode key) {
			return Open && (key == KeyCode.UpArrow || key == KeyCode.DownArrow
				|| key == KeyCode.LeftArrow || key == KeyCode.RightArrow);
		}
	}
}
