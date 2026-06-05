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
		private static readonly FieldInfo InstructionsField = AccessTools.Field(typeof(UIEncounterEventPanel), "m_instructionsText");

		/// <summary>
		/// The live narrative (scenario, then result after a choice) and the mechanical
		/// instructions (what the choice triggers: draw cards, win a token). Both are
		/// raw label text; composition and ordering are decided in Core. Set to null
		/// when no panel is live.
		/// </summary>
		public static void Read(out string narrative, out string instructions) {
			narrative = null;
			instructions = null;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			UIEncounterEventPanel panel = ui.EncounterEventPanel;
			if (panel == null) return;
			narrative = LabelText(TextField, panel);
			instructions = LabelText(InstructionsField, panel);
		}

		private static string LabelText(FieldInfo field, UIEncounterEventPanel panel) {
			var label = (UILabel)field.GetValue(panel);
			return label != null ? label.text : null;
		}
	}
}
