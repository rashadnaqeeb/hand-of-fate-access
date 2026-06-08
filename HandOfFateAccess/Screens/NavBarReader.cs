using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the primary nav bar's two context-action buttons (Function0/Function1) when they
	/// are shown. These carry screen-specific actions the game maps to controller face buttons
	/// and function keys (the scoreboard's high-score toggle and restart, the deck builder's
	/// fill-deck...), which sit outside the focusable selection so the focus path never reaches
	/// them. Confirm and Cancel (Select/Back) are deliberately not read: they restate per
	/// selection and would be chatter. The button's label is reached by reflection so a
	/// game-side rename crashes the build's audit rather than degrading silently.
	/// </summary>
	internal static class NavBarReader {
		private static readonly FieldInfo LabelsField = AccessTools.Field(typeof(NavBarButton), "m_labels");

		/// <summary>The shown context-action labels, or null when no nav bar is live.</summary>
		public static List<string> ReadActions() {
			UIManager ui = UIManager.Instance;
			MainNavBar bar = ui != null ? ui.PrimaryNavBar : null;
			if (bar == null) return null;
			var actions = new List<string>();
			Add(actions, bar.Function0Button);
			Add(actions, bar.Function1Button);
			return actions;
		}

		private static void Add(List<string> actions, NavBarButton button) {
			if (button == null || !button.IsShowing) return;
			var labels = (UILabel[])LabelsField.GetValue(button);
			if (labels == null) return;
			foreach (UILabel label in labels)
				if (label != null && !string.IsNullOrEmpty(label.text)) {
					actions.Add(label.text);
					return;
				}
		}
	}
}
