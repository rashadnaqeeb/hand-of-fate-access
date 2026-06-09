namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// One-frame flag set when the chance shuffle coroutine starts (the AnimatedShuffle hook),
	/// consumed by the gambit pump to begin following the cards. The hook only records; the
	/// pump starts the sustained tones. Single Unity thread, so no synchronization.
	/// </summary>
	internal static class ChanceShuffleSignal {
		private static bool _started;

		public static void RecordStart() => _started = true;

		public static bool ConsumeStart() {
			bool value = _started;
			_started = false;
			return value;
		}
	}
}
