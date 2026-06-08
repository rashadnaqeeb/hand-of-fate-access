using HandOfFateAccess.Focus;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on CardSetModifierContainer.OnCardClicked, the handler fired when the
	/// player clicks a face-down reward card on the end-of-run reward screen. The game
	/// flips the card face-up and moves focus to the next card, so the focus path never
	/// reads the revealed reward. Record the flipped card for the Update pump to announce.
	/// By postfix time the card's Flipped flag is already cleared, so the pump reads its
	/// real identity rather than "face down card". Records only; speaking is the pump's job.
	/// </summary>
	internal static class CardSetModifierContainer_OnCardClicked_Patch {
		private static void Postfix(Card a_card) => RewardReveal.Record(a_card);
	}
}
