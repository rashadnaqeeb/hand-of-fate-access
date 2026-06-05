using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the cabinet's examine panel: the section the player is viewing (lore, deck
	/// changes, upgrades...) and that section's body text. Pressing a court card opens a
	/// CabinetCardInfo panel that is not a MenuManager push and carries no focusable
	/// control, so the focus model never reaches it; the screen watcher edge-polls this
	/// and announces it. The court card itself stays focused and speaks its own name, so
	/// only the section detail is read here.
	///
	/// Reached from the live StartOptions singleton, then the private CabinetCardInfo
	/// reference and its current panel index, by reflection so a game-side rename crashes
	/// the build's audit rather than degrading silently. The panels animate via alpha
	/// rather than activation, so the active section is found by index, not by sweeping
	/// active objects; labels within that one panel are read live at speech time.
	/// </summary>
	internal static class CabinetReader {
		private static readonly FieldInfo CourtCardInfoField = AccessTools.Field(typeof(StartOptionsContainer), "m_courtCardInfo");
		private static readonly FieldInfo PanelIndexField = AccessTools.Field(typeof(CabinetCardInfo), "m_panelIndex");
		private static readonly FieldInfo PanelsField = AccessTools.Field(typeof(CabinetCardInfo), "m_panels");

		/// <summary>
		/// The active section title and its body labels, or nulls when no panel is shown
		/// (panel index below zero). Cheap index check first, so the label sweep only runs
		/// while the panel is actually open.
		/// </summary>
		public static void Read(out string section, out string[] body) {
			section = null;
			body = null;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			StartOptions start = ui.StartOptions;
			if (start == null) return;
			StartOptionsContainer container = start.Container;
			if (container == null) return;
			var info = (CabinetCardInfo)CourtCardInfoField.GetValue(container);
			if (info == null) return;

			int idx = (int)PanelIndexField.GetValue(info);
			var panels = (CabinetCardInfoPanel[])PanelsField.GetValue(info);
			if (idx < 0 || panels == null || idx >= panels.Length) return;
			CabinetCardInfoPanel panel = panels[idx];
			if (panel == null) return;

			// Title is a serialized string (already display text or a key); GetString
			// localizes a key and returns plain text unchanged.
			section = UIUtils.GetString(panel.Title);
			UILabel[] labels = panel.GetComponentsInChildren<UILabel>(includeInactive: false);
			body = new string[labels.Length];
			for (int i = 0; i < labels.Length; i++)
				body[i] = labels[i].text;
		}
	}
}
