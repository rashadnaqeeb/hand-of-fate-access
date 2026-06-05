using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the encounter event panel's spoken line from its display-only labels:
	/// the narrative (scenario, then the result story after a choice) and the mechanical
	/// instructions (what the choice triggers, e.g. "Draw 1 Equipment Card", "This
	/// card's token is now yours"). Narrative first, then instructions, each only if
	/// present; markup stripping and empty-dropping run through the Message pipeline.
	/// Returns "" when nothing is left to say.
	///
	/// The opening scenario is, by the game's own definition, the encounter card's
	/// description, which the player already heard on focusing the card. When the
	/// narrative matches that description it is dropped to avoid reading it twice; a
	/// later event's scenario or a choice result differs from the card and is kept.
	/// </summary>
	public static class EncounterNarration {
		public static string Compose(string narrative, string instructions, string cardDescription) {
			if (!string.IsNullOrEmpty(narrative) && narrative == cardDescription)
				narrative = null;
			return new Message().Add(narrative).Add(instructions).Resolve();
		}
	}
}
