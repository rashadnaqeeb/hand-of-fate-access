using System.Reflection;
using HarmonyLib;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the live narrative on the encounter event panel: the scenario prompt when
	/// an event opens, then the result text after a choice is made (the encounter
	/// resolution). Both are display-only UILabel text that no control ever focuses, so
	/// the focus model never reaches them; the screen watcher edge-polls this and
	/// announces it. The private label field is reached by reflection so a game-side
	/// rename crashes the build's audit rather than degrading silently.
	/// </summary>
	internal static class EncounterEventReader {
		private static readonly FieldInfo TextField = AccessTools.Field(typeof(UIEncounterEventPanel), "m_encounterEventText");

		/// <summary>The current rendered narrative, or null when no panel/text is live.</summary>
		public static string Read() {
			UIManager ui = UIManager.Instance;
			if (ui == null) return null;
			UIEncounterEventPanel panel = ui.EncounterEventPanel;
			if (panel == null) return null;
			var label = (UILabel)TextField.GetValue(panel);
			return label != null ? label.text : null;
		}
	}
}
