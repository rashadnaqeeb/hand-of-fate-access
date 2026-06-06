using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the deck-builder Fates pile's spoken lines from its display-only info panel.
	/// The focused archetype card is suppressed (the panel owns the readout), so this speaks
	/// both the card identity and the active section. The name reads when the selected
	/// archetype changes; the section (Description or Loadout) reads when it changes, whether
	/// from selecting a new archetype or paging the panel. Markup stripping, empty-dropping
	/// and duplicate collapsing run through the Message pipeline; both return "" when empty.
	/// </summary>
	public static class ArchetypeNarration {
		/// <summary>The archetype's name, with "locked" appended when it is not yet unlocked.</summary>
		public static string ComposeName(string name, bool locked) {
			var message = new Message().Add(name);
			// "locked" qualifies a name; with no name there is nothing to qualify, so say
			// nothing rather than a bare "locked".
			if (locked && message.Resolve().Length > 0)
				message.Add(Strings.CardLocked);
			return message.Resolve();
		}

		/// <summary>The active section's title then its body labels in hierarchy order.</summary>
		public static string ComposeSection(string section, string[] body) {
			return new Message().Add(section).AddRange(body).Resolve();
		}
	}
}
