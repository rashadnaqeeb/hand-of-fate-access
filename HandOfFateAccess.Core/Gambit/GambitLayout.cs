namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Maps a card's slot to its stereo position. The chance cards lie left to right across
	/// the field; the pan is the only spatial cue a blind player has for which card is which,
	/// so it is computed here (testable) rather than read off a transform.
	/// </summary>
	public static class GambitLayout {
		/// <summary>Pan for slot <paramref name="index"/> of <paramref name="count"/> cards,
		/// spread evenly from -1 (hard left) to +1 (hard right). A single card sits centred.</summary>
		public static float SlotPan(int index, int count) {
			if (count <= 1) return 0f;
			if (index <= 0) return -1f;
			if (index >= count - 1) return 1f;
			return -1f + 2f * index / (count - 1);
		}
	}
}
