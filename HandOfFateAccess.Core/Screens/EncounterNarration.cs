using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the encounter event panel's spoken line from its display-only labels:
	/// the narrative (scenario, then the result story after a choice) and the mechanical
	/// instructions (what the choice triggers, e.g. "Draw 1 Equipment Card", "This
	/// card's token is now yours"). Narrative first, then instructions, each only if
	/// present; markup stripping and empty-dropping run through the Message pipeline.
	/// Returns "" when nothing is left to say. The encounter card's focus readout omits
	/// the scenario, so the panel is its single source and there is no duplication.
	/// </summary>
	public static class EncounterNarration {
		public static string Compose(string narrative, string instructions) {
			return new Message().Add(narrative).Add(instructions).Resolve();
		}

		/// <summary>
		/// Decides what (if anything) to announce this frame from the panel's two live
		/// labels, given the narrative and instructions last announced. The narrative (the
		/// story beat) and the instructions (the mechanical line: "draws you 4 cards", "win
		/// a token") are set on the label in separate frames: a choice resolves, the result
		/// narrative shows alone, then the player confirms and the instructions are appended
		/// while the narrative stays put. Re-reading the whole composed line then repeats the
		/// narrative the player already heard. So a fresh narrative is spoken with whatever
		/// instructions accompany it, but when only the instructions change under an
		/// unchanged narrative just the instructions are spoken. The markers are held through
		/// the sub-second blank the game flashes mid-transition (both labels empty) and
		/// re-armed only when the panel stops showing, so the next encounter announces afresh
		/// even if its first beat repeats this one's last.
		/// </summary>
		public static EncounterAnnouncement Decide(bool showing, string narrative, string instructions,
				string lastNarrative, string lastInstructions) {
			if (!showing)
				return new EncounterAnnouncement(false, null, null, null);
			narrative = Nullify(narrative);
			instructions = Nullify(instructions);
			if (narrative != null && narrative != lastNarrative)
				return new EncounterAnnouncement(true, Compose(narrative, instructions), narrative, instructions);
			if (instructions != null && instructions != lastInstructions && narrative == lastNarrative)
				return new EncounterAnnouncement(true, Compose(null, instructions), lastNarrative, instructions);
			return new EncounterAnnouncement(false, null, lastNarrative, lastInstructions);
		}

		private static string Nullify(string s) => string.IsNullOrEmpty(s) ? null : s;
	}

	/// <summary>
	/// The pump's verdict for one frame: whether to speak, the line to speak, and the
	/// narrative/instructions markers to store. The markers are the narrative and
	/// instructions last announced; they are held unchanged through a transient blank and
	/// both null when the panel is down.
	/// </summary>
	public struct EncounterAnnouncement {
		public readonly bool Speak;
		public readonly string Text;
		public readonly string MarkerNarrative;
		public readonly string MarkerInstructions;

		public EncounterAnnouncement(bool speak, string text, string markerNarrative, string markerInstructions) {
			Speak = speak;
			Text = text;
			MarkerNarrative = markerNarrative;
			MarkerInstructions = markerInstructions;
		}
	}
}
