namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// One-frame flag set by the FlipCards hook when the chance cards flip face up, consumed
	/// by the gambit pump. The hook only records; the pump decides whether these are chance
	/// cards and runs the Establish walk, keeping all the work off the Harmony patch. Single
	/// Unity thread, so no synchronization.
	/// </summary>
	internal static class ChanceFlipSignal {
		private static bool _faceUp;

		public static void RecordFaceUp() => _faceUp = true;

		public static bool ConsumeFaceUp() {
			bool value = _faceUp;
			_faceUp = false;
			return value;
		}
	}
}
