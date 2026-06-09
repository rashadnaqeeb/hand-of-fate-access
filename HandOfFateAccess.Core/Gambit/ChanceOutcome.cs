namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// The four outcomes a chance card can carry, mirroring the game's ChanceType enum
	/// (Success, HugeSuccess, Failure, HugeFailure). Core cannot reference the game
	/// assembly, so the gambit adapter in the plugin translates each live ChanceType into
	/// this mod-side value, then resolves the spoken word from the game's own localized
	/// outcome title (CHANCE_TITLE_*) so it reads in the player's language.
	/// </summary>
	public enum ChanceOutcome {
		Success,
		HugeSuccess,
		Failure,
		HugeFailure,
	}
}
