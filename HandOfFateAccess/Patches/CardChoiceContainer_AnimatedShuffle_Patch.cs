using HandOfFateAccess.Gambit;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on CardChoiceContainer.AnimatedShuffle, an iterator method, so the postfix runs
	/// when the shuffle coroutine is created, just as the shuffle begins. Flags it for the
	/// gambit pump, which starts each card's sustained tone and follows it through the moves.
	/// Records only; starting the tones is the pump's job.
	/// </summary>
	internal static class CardChoiceContainer_AnimatedShuffle_Patch {
		private static void Postfix() => ChanceShuffleSignal.RecordStart();
	}
}
