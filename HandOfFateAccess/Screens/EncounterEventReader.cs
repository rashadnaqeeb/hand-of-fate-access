using System.Reflection;
using HandOfFateAccess.Focus;
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
		/// The live narrative (scenario, then result after a choice), the mechanical
		/// instructions (what the choice triggers: draw cards, win a token), and the
		/// description of the encounter card the player most recently heard. All raw
		/// text; composition and the duplicate-scenario decision are made in Core. The
		/// card description lets Core drop the opening scenario, which is defined as that
		/// same description and was already spoken on focusing the card. Anchoring on the
		/// card actually heard (not the active encounter's card) means an encounter the
		/// player never focused still announces its scenario. Null when no panel is live.
		/// </summary>
		public static void Read(out string narrative, out string instructions, out string cardDescription) {
			narrative = null;
			instructions = null;
			cardDescription = null;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			UIEncounterEventPanel panel = ui.EncounterEventPanel;
			if (panel == null) return;
			narrative = LabelText(TextField, panel);
			instructions = LabelText(InstructionsField, panel);
			EncounterCard heard = ProxyFactory.LastAnnouncedEncounterCard;
			if (heard != null)
				cardDescription = heard.LocalisedDescription;
		}

		private static string LabelText(FieldInfo field, UIEncounterEventPanel panel) {
			var label = (UILabel)field.GetValue(panel);
			return label != null ? label.text : null;
		}
	}
}
