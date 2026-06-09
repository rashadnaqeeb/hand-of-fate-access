using HandOfFateAccess.Util;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Maps a chance outcome to the game's own localized title key (CHANCE_TITLE_*), the single
	/// place the gambit names an outcome. The status spoken during the establish walk and the
	/// outcome announced when the player commits both resolve their text from here, so a game-side
	/// key rename is one audit point. An out-of-range value returns a key absent from every locale,
	/// so the lookup leaves it unspoken rather than naming a wrong outcome.
	/// </summary>
	public static class ChanceOutcomeKeys {
		public static string Title(ChanceOutcome outcome) {
			switch (outcome) {
				case ChanceOutcome.Success: return "CHANCE_TITLE_SUCCESS";
				case ChanceOutcome.HugeSuccess: return "CHANCE_TITLE_HUGE_SUCCESS";
				case ChanceOutcome.Failure: return "CHANCE_TITLE_FAILURE";
				case ChanceOutcome.HugeFailure: return "CHANCE_TITLE_HUGE_FAILURE";
				default:
					Log.Error($"unmapped chance outcome {(int)outcome}; leaving it silent");
					return "CHANCE_TITLE_UNMAPPED";
			}
		}
	}
}
