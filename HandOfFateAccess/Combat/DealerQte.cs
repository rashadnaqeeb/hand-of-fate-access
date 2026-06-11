namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Whether the Dealer's missile quick-time event is running on the player. The
	/// reaction action teleports the player to an authored missile platform (and leaves
	/// them there; nothing moves them back) and overrides the camera, so every
	/// positional sound would read nonsense there: zone/beacon/projectile bearings
	/// project through a camera that no longer frames the fight, and the wall probe
	/// measures from wherever the platform sits, which says nothing about where the
	/// player can walk. The environmental voices treat the event like pause and go
	/// quiet; the attack-cue pump stays live because it carries the event's own counter
	/// and dodge prompts.
	///
	/// The Begin patch records the live action; activity is the action's own IsRunning,
	/// re-read at every ask, so a Complete this class never hears about (or the action's
	/// destruction at level end) opens the gate by itself.
	/// </summary>
	internal static class DealerQte {
		private static ActionDealerMissileReaction s_reaction;

		/// <summary>Record the reaction action as its Begin runs. Called from the Harmony
		/// postfix; records only, per the hook rule.</summary>
		public static void Record(ActionDealerMissileReaction reaction) {
			s_reaction = reaction;
		}

		public static bool IsActive => s_reaction != null && s_reaction.IsRunning;
	}
}
