namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Whether a hazard source can hurt the player: the one predicate every hazard filter
	/// (projectiles, movers, areas, beams, chains) shares, so the rule cannot drift per
	/// feature. Enemy is obvious; a Neutral-team source treats BOTH sides as enemies
	/// (Targetable.GetEnemyTeams maps Neutral to Enemy|Player), so its hazards damage the
	/// player and must be voiced. A None-team or sourceless effect cannot pass the game's
	/// IsTarget check for anyone, and the player's own team is not a threat to itself.
	/// </summary>
	internal static class Hostility {
		public static bool ThreatensPlayer(Targetable source) {
			return source != null
				&& (source.Team == TeamType.Enemy || source.Team == TeamType.Neutral);
		}
	}
}
