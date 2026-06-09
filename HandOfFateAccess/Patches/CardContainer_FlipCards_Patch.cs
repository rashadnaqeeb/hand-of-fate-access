using HandOfFateAccess.Gambit;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on CardContainer.FlipCards, which flips a container's cards face up (a_tf false)
	/// or face down (a_tf true). The chance gambit reveals its cards face up here, just before
	/// the held shuffle, so a face-up flip on the choice container is the cue to run the
	/// Establish walk. Records only when it is the choice container; the pump confirms the
	/// cards are chance cards and does the work. Speaking and freezing are the pump's job.
	/// </summary>
	internal static class CardContainer_FlipCards_Patch {
		private static void Postfix(CardContainer __instance, bool a_tf) {
			if (a_tf) return;
			DeckManager deckManager = DeckManager.Instance;
			if (deckManager == null || __instance != deckManager.CardChoiceContainer) return;
			ChanceFlipSignal.RecordFaceUp();
		}
	}
}
