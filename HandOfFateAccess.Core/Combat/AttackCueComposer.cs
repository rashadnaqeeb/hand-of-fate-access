using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears for an incoming enemy attack: which telegraph sample
	/// plays and where it sits in the stereo field. One cue per attack, positioned at the
	/// attacker, fired the moment the attack's parry window opens. That is when the game shows
	/// its own block-or-dodge flash and slows the windup animation to a crawl, and the whole
	/// slowed windup is the reaction time, so this single moment carries both "attack incoming
	/// from there" and WHAT to do: block (a reflect for a ranged shot) or dodge, depending on
	/// whether the attack is blockable.
	///
	/// The "what to do" lives entirely in the sample identity, never in modulation: block and
	/// dodge demand opposite reactions, so they must be impossible to confuse under pressure
	/// and are two timbrally distinct authored sounds. Only position (pan/pitch/volume) is
	/// modulated. This is the "what the player hears" decision, so it lives in Core and is
	/// unit-tested off-engine; the plugin adapter only reads the attacker's offset off its live
	/// transform and feeds the two numbers in.
	///
	/// Bearing rides the shared placement law (<see cref="Spatializer"/>), the same
	/// grammar as projectile sonification so the player learns one spatial language for
	/// the whole fight: east/west (camera-right) is the stereo position, and fore/aft is
	/// the down-biased pitch, darker meaning behind. The block/dodge contrast is timbral
	/// identity, so it survives the placement shift at every bearing. Volume carries distance, but a
	/// telegraph is a critical alert rather than ambient tracking, so it stays loud
	/// throughout and only tapers to a high floor far off, never to a faint one.
	/// </summary>
	public static class AttackCueComposer {
		/// <summary>Sample key for a blockable attack: block it, or reflect it if ranged.</summary>
		public const string BlockKey = "attack_block";

		/// <summary>Sample key for an unblockable attack: dodge, never block.</summary>
		public const string DodgeKey = "attack_dodge";

		/// <summary>Distance, in world units, over which volume falls from its peak to the
		/// far floor. Roughly an arena's engagement reach.</summary>
		public const float FalloffRange = 16f;

		/// <summary>Loudness when the attacker is on top of the player.</summary>
		public const float MaxVolume = 1f;

		/// <summary>Loudness at and beyond <see cref="FalloffRange"/>. A high floor, not a
		/// faint one: an attack telegraph is a reaction-critical alert that must be heard
		/// even from across the arena, not ambient detail that can fade.</summary>
		public const float MinVolume = 0.55f;

		/// <summary>The sample key for an attack's cue: <see cref="BlockKey"/> only when the
		/// attack can be blocked or reflected AND the player currently holds the matching
		/// ability (<paramref name="canBlock"/>: a counter for melee, a reflect for ranged);
		/// otherwise <see cref="DodgeKey"/>. The cue is an action instruction, and a nominally
		/// blockable attack is unblockable for a player without the ability, so it must say
		/// dodge: a block cue the player cannot act on is a guaranteed hit.</summary>
		public static string ActionKey(bool blockable, bool canBlock) =>
			blockable && canBlock ? BlockKey : DodgeKey;

		/// <summary>Half-width, in world units, of the corridor a straight shot threatens:
		/// the player's body plus a projectile's width, erring wide (over-warning is the
		/// safe direction).</summary>
		public const float LaneHalfWidth = 1.5f;

		/// <summary>
		/// Whether a straight shot spawned at (<paramref name="right"/>,
		/// <paramref name="forward"/>) (its offset from the player) travelling along the
		/// ground direction (<paramref name="dirRight"/>, <paramref name="dirForward"/>,
		/// any length) threatens the player: its path passes within
		/// <see cref="LaneHalfWidth"/> of them and it is travelling toward them, not away.
		/// The trap-shot cue's filter: a spear wall fires its lanes on a timer forever,
		/// whether or not anyone stands in them, so only a shot whose lane covers the
		/// player is worth an alert - cueing every volley of every wall would be a wall of
		/// noise. A degenerate direction never threatens.
		/// </summary>
		public static bool ShotThreatens(float right, float forward, float dirRight, float dirForward) {
			float lengthSq = dirRight * dirRight + dirForward * dirForward;
			if (lengthSq < 1e-9f) return false;
			float length = (float)Math.Sqrt(lengthSq);
			float unitRight = dirRight / length;
			float unitForward = dirForward / length;

			// The player (the origin) projected onto the shot's ray: a non-positive travel
			// distance means the closest approach is behind the spawn, a shot moving away.
			float travel = -(right * unitRight + forward * unitForward);
			if (travel <= 0f) return false;

			float missRight = right + travel * unitRight;
			float missForward = forward + travel * unitForward;
			return missRight * missRight + missForward * missForward < LaneHalfWidth * LaneHalfWidth;
		}

		/// <summary>
		/// The voice parameters for an attacker sitting <paramref name="right"/> world units to
		/// the camera's right (negative is left) and <paramref name="forward"/> world units
		/// toward screen-north (negative is south) of the player. Pan and pitch encode its
		/// bearing (pure bearing past the near-field crossover); volume encodes its ground
		/// distance.
		/// </summary>
		public static SoundParams Compose(float right, float forward) {
			float distance = (float)Math.Sqrt(right * right + forward * forward);
			return Spatializer.Place(right, forward, VolumeFor(distance));
		}

		/// <summary>Volume for a ground distance: <see cref="MaxVolume"/> at zero, falling
		/// linearly to <see cref="MinVolume"/> at <see cref="FalloffRange"/> and holding that
		/// floor beyond. A non-finite distance also yields the floor.</summary>
		public static float VolumeFor(float distance) {
			if (distance <= 0f) return MaxVolume;
			// NaN fails this comparison too, so a degenerate distance drops to the floor
			// rather than blasting at full volume.
			if (!(distance < FalloffRange)) return MinVolume;
			return MaxVolume + (MinVolume - MaxVolume) * (distance / FalloffRange);
		}
	}
}
