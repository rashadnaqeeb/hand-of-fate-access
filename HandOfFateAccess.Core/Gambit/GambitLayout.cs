namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Maps a card's slot to its stereo position. The chance cards lie left to right across
	/// the field; the pan is the only spatial cue a blind player has for which card is which,
	/// so it is computed here (testable) rather than read off a transform.
	/// </summary>
	public static class GambitLayout {
		/// <summary>
		/// Pan for slot <paramref name="index"/> of <paramref name="count"/> cards, hard left to
		/// hard right. The inner cards are pushed outward (the validated prototype used
		/// [-1, -0.5, 0.5, 1] for four cards, not an even [-1, -0.33, 0.33, 1]) so adjacent cards
		/// are easier to tell apart and track. The ends always sit hard left/right; a single card
		/// is centred.
		/// </summary>
		public static float SlotPan(int index, int count) {
			if (count <= 1) return 0f;
			if (count == 2) return index <= 0 ? -1f : 1f;
			// Spread over count-2 (not count-1) so the two end cards land past +-1 and clamp
			// there, widening the gaps between the inner cards. For four cards this yields
			// -1, -0.5, 0.5, 1.
			float pan = (2f * index - (count - 1)) / (count - 2);
			return pan < -1f ? -1f : (pan > 1f ? 1f : pan);
		}
	}
}
