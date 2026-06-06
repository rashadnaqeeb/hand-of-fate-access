namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the deck-builder Fates pile's info panel: the selected archetype's name, whether
	/// it is locked, and the active section (Description or Loadout) with its body labels. The
	/// archetype browser shows the same CabinetCardInfo the court cabinet uses, a display-only
	/// panel with no focusable control, so the focus model never reaches it; the focused
	/// archetype card itself is suppressed (it would only re-read the title and duplicate the
	/// panel's description). The section read is shared with the cabinet via CardInfoPanelReader.
	///
	/// Sourced from the live UIDeckBuilder: the active mode's browser, when it is the archetype
	/// browser. Returns nulls and false outside the Fates pile, so the caller says nothing there.
	/// </summary>
	internal static class ArchetypeReader {
		public static void Read(out string cardName, out bool locked, out string section, out string[] body) {
			cardName = null;
			locked = false;
			section = null;
			body = null;

			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			UIDeckBuilder deckBuilder = ui.DeckBuilder;
			if (deckBuilder == null) return;
			DeckBuilderMode mode = deckBuilder.ActiveMode;
			ArchetypeBrowser browser = mode != null ? mode.Browser as ArchetypeBrowser : null;
			if (browser == null) return;

			Card card = CardInfoPanelReader.Read(browser.InfoPanel, out section, out body);
			if (card == null) return;

			// Card.Title is a localization key; GetString localizes it (and returns plain text
			// unchanged). A locked archetype is darkened with its action set to buy; IsUnlocked
			// is the same flag the browser's darken keys off.
			cardName = UIUtils.GetString(card.Title);
			var archetype = card as ArchetypeCard;
			locked = archetype != null && !archetype.ArchetypePrefab.IsUnlocked;
		}
	}
}
