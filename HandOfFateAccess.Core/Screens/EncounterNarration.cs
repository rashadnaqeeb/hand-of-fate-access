using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the encounter event panel's spoken line from its two display-only
	/// labels: the narrative (scenario, then the result story after a choice) and the
	/// mechanical instructions (what the choice triggers, e.g. "Draw 1 Equipment Card",
	/// "This card's token is now yours"). Narrative first, then instructions, each only
	/// if present; markup stripping and empty-dropping run through the Message pipeline.
	/// Returns "" when neither is present.
	/// </summary>
	public static class EncounterNarration {
		public static string Compose(string narrative, string instructions) {
			return new Message().Add(narrative).Add(instructions).Resolve();
		}
	}
}
