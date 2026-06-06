using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the active section of a CabinetCardInfo, the multi-section display-only detail
	/// panel used by both the court cabinet and the deck builder's Fates pile. The panels
	/// animate via alpha rather than activation, so the shown section is found by the panel's
	/// own index, not by sweeping active objects; the labels within that one section are read
	/// live at speech time. Returns the examined card so callers can read its name and any
	/// per-context state; section and body are null when no section is shown.
	///
	/// The index and panel-array fields are reached by reflection so a game-side rename
	/// crashes the build's audit rather than degrading silently.
	/// </summary>
	internal static class CardInfoPanelReader {
		private static readonly FieldInfo PanelIndexField = AccessTools.Field(typeof(CabinetCardInfo), "m_panelIndex");
		private static readonly FieldInfo PanelsField = AccessTools.Field(typeof(CabinetCardInfo), "m_panels");

		public static Card Read(CabinetCardInfo info, out string section, out string[] body) {
			section = null;
			body = null;
			if (info == null) return null;

			int idx = (int)PanelIndexField.GetValue(info);
			var panels = (CabinetCardInfoPanel[])PanelsField.GetValue(info);
			if (idx < 0 || panels == null || idx >= panels.Length) return null;
			CabinetCardInfoPanel panel = panels[idx];
			if (panel == null) return null;

			// Title is a serialized string (display text or a key); GetString localizes a key
			// and returns plain text unchanged.
			section = UIUtils.GetString(panel.Title);

			// A deck-change or starting card is a CardInfo2D: its own title and description
			// labels alongside an embedded real card whose template re-renders the same name
			// and effect. Reading both says each card two or three times, so skip any label
			// inside an embedded card template and keep only the panel's own summary labels.
			//
			// A single label can hold several newline-separated lines (the starting-resources
			// block is one label of "- Health 100\n- Max Health 100\n..."); split them so each
			// reads as its own item rather than one run-on. A line with no letter or digit (a
			// stray bullet, or a content-less card summary like a modifier's placeholder ".")
			// carries nothing spoken, so drop it.
			UILabel[] labels = panel.GetComponentsInChildren<UILabel>(includeInactive: false);
			var kept = new List<string>(labels.Length);
			foreach (UILabel label in labels) {
				if (label.GetComponentInParent<CardTemplate>() != null)
					continue;
				foreach (string line in label.text.Split('\n')) {
					string trimmed = line.Trim();
					if (HasContent(trimmed))
						kept.Add(trimmed);
				}
			}
			body = kept.ToArray();
			return info.Card;
		}

		private static bool HasContent(string text) {
			foreach (char c in text)
				if (char.IsLetterOrDigit(c))
					return true;
			return false;
		}
	}
}
