using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the cabinet's examine panel: the examined card's name and the section the
	/// player is viewing (lore, deck changes, upgrades...) with that section's body text.
	/// Pressing a court card opens a CabinetCardInfo panel that is not a MenuManager push
	/// and carries no focusable control, so the focus model never reaches it; the screen
	/// watcher edge-polls this and announces it. The card name sits on the panel's nav bar
	/// (shown on every section, even for a card face-down on the rack), which the focus
	/// path never reads, so it is read here and announced once when the examined card changes.
	///
	/// Reached from the live StartOptions singleton, then the private CabinetCardInfo
	/// reference, by reflection so a game-side rename crashes the build's audit rather than
	/// degrading silently. The section read itself is shared with the Fates pile via
	/// <see cref="CardInfoPanelReader"/>; this only sources the cabinet's panel and the name.
	/// </summary>
	internal static class CabinetReader {
		private static readonly FieldInfo CourtCardInfoField = AccessTools.Field(typeof(StartOptionsContainer), "m_courtCardInfo");

		/// <summary>
		/// The examined card's name, the active section title, and its body labels, or nulls
		/// when no panel is shown. The examine banner shows the name on every section (even for
		/// a card face-down on the rack), which the focus path never reads, so it is sourced here.
		/// </summary>
		public static void Read(out string cardName, out string section, out string[] body) {
			cardName = null;
			section = null;
			body = null;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			StartOptions start = ui.StartOptions;
			if (start == null) return;
			StartOptionsContainer container = start.Container;
			if (container == null) return;
			var info = (CabinetCardInfo)CourtCardInfoField.GetValue(container);

			// Card.Title is a localization key; GetString localizes it (and returns plain text
			// unchanged).
			Card card = CardInfoPanelReader.Read(info, out section, out body);
			if (card != null)
				cardName = UIUtils.GetString(card.Title);
		}
	}
}
