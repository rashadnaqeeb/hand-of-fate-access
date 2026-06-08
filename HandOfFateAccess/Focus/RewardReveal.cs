namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Shared state between the OnCardClicked hook (producer) and the Update pump
	/// (consumer) for the end-of-run reward reveal. When the player clicks a face-down
	/// reward card, the game flips it face-up and moves focus straight to the next card,
	/// so the focus path never reads what was revealed. The hook records the flipped
	/// card here; the pump reads its identity and speaks it, per the announce-from-update
	/// rule (the hook only records, never speaks).
	/// </summary>
	internal static class RewardReveal {
		private static Card _pending;

		/// <summary>Called from the hook when a reward card is flipped face-up.</summary>
		public static void Record(Card card) => _pending = card;

		/// <summary>
		/// Hand back the freshly revealed card and clear it, or false when none is
		/// pending. The card is held as a live reference and read at consume time.
		/// </summary>
		public static bool TryConsume(out Card card) {
			card = _pending;
			_pending = null;
			return card != null;
		}
	}
}
