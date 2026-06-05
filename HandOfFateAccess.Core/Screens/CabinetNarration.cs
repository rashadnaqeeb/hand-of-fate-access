using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the cabinet examine panel's spoken line from its display-only labels:
	/// the section title (lore, deck changes, upgrades...) then that section's body
	/// labels in hierarchy order. The focused court card already speaks its name, so the
	/// panel adds only the section detail and there is no duplication. Markup stripping,
	/// empty-dropping, and duplicate collapsing run through the Message pipeline; returns
	/// "" when nothing is left to say (panel hidden or empty).
	/// </summary>
	public static class CabinetNarration {
		public static string Compose(string section, string[] body) {
			return new Message().Add(section).AddRange(body).Resolve();
		}
	}
}
