namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The one distance-to-closeness mapping every positioned combat sound ranges by:
	/// full inside the player's default melee reach (this is within a swing of you),
	/// falling linearly to nothing at arena scale. First validated in play as the enemy
	/// locator's loudness curve, then unified: loudness (and the beacons' ping cadence)
	/// must mean the SAME distance whatever the sound, or the player has to carry one
	/// mental curve per family. Each family maps the closeness fraction onto its own
	/// loudness identity - the locator 1 down to a faint 0.1, the beacons 0.7 down to the
	/// same faint floor, the zone loops 0.9 down to true silence (a far hazard is not a
	/// live threat, and silence IS the all-clear).
	/// </summary>
	public static class RangingCurve {
		/// <summary>Ground distance, in world units, at and inside which a sound ranges as
		/// fully close: the game's default melee attack range
		/// (PlayerCombat.m_defaultAttackRange), so full means "in swing reach".</summary>
		public const float FullRange = 3f;

		/// <summary>Ground distance, in world units, at and beyond which a sound ranges as
		/// fully far.</summary>
		public const float FloorRange = 20f;

		/// <summary>The closeness fraction for a ground distance: 1 at and inside
		/// <see cref="FullRange"/>, falling linearly to 0 at <see cref="FloorRange"/> and
		/// holding there beyond. A non-finite distance reads as far, never as close.</summary>
		public static float Closeness(float distance) {
			if (distance <= FullRange) return 1f;
			// NaN fails this comparison too, so a degenerate distance reads far.
			if (!(distance < FloorRange)) return 0f;
			return (FloorRange - distance) / (FloorRange - FullRange);
		}
	}
}
