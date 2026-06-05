using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the death/forfeit context line shown on the results screen ("you ran out of
	/// health", "you forfeited the journey"). UIDeathPanel sets this on a display-only
	/// label and is wired entirely through its prefab (no code caller), so the focus
	/// model never reaches it; the screen watcher edge-polls this on the results state and
	/// announces it. The instance is found in the live death-menu hierarchy; the label
	/// field is reached by reflection so a game-side rename crashes the build's audit
	/// rather than degrading silently.
	/// </summary>
	internal static class DeathPanelReader {
		private static readonly FieldInfo ContextTextField = AccessTools.Field(typeof(UIDeathPanel), "m_contextText");

		/// <summary>The live context text, or null when no death panel is present.</summary>
		public static string Read() {
			UIDeathPanel panel = Object.FindObjectOfType<UIDeathPanel>();
			if (panel == null) return null;
			var label = (UILabel)ContextTextField.GetValue(panel);
			return label != null ? label.text : null;
		}
	}
}
