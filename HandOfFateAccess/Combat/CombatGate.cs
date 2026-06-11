namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Whether a fight is live for the combat sounds: the encounter exists and the game
	/// is in level play. The game enters <c>GameState_Level_Play</c> only when the
	/// level-in transition finishes (completed or skipped), leaves it for
	/// <c>GameState_Level_Out</c> in the same call stack as the encounter's complete or
	/// fail event, and swaps it for <c>GameState_Level_Pause</c> while the pause menu is
	/// up, so this one read starts the sounds at the handoff to the player, stops them
	/// the instant the fight resolves, and silences them across pause.
	///
	/// This gates the combat features individually, never the audio engine: the sound
	/// glossary plays its demos through the same engine while the game is paused, and
	/// the gambit's audio runs at the card table, outside this gate entirely.
	/// </summary>
	internal static class CombatGate {
		public static bool IsLive =>
			CombatEncounter.Instance != null
			&& Game.Instance.ActiveGameState is GameState_Level_Play;

		/// <summary>A trap room: the game wired a <c>TrapExit</c> as this level's completion
		/// condition (decided once at the encounter's start, stable for the level). The
		/// dense-gauntlet levels where several sounds trade legibility for restraint:
		/// treasure answers the locator key instead of pinging, and only the single nearest
		/// hazard holds a zone voice. Not <c>Trap.LevelHasTraps</c>, which is also true in
		/// arena fights that merely contain a trap.</summary>
		public static bool IsTrapRoom {
			get {
				CombatEncounter encounter = CombatEncounter.Instance;
				return encounter != null && encounter.HasTrapExit;
			}
		}
	}
}
