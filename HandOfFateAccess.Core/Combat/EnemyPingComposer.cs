using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears for an enemy-locator ping: the bearing comes from the
	/// projectile grammar (<see cref="ProjectileSonifier"/>: pan = east/west, down-biased
	/// pitch = north/south), so the learned spatial language transfers, but the loudness is
	/// the locator's own ranging curve, wider than either the telegraph alert's or the
	/// projectile loop's. The ping is a one-shot answer to "where is the enemy", so its
	/// volume is the distance readout: full inside the player's default melee reach (the
	/// answer is "swing now"), tapering to a quiet-but-audible floor across the arena.
	/// </summary>
	public static class EnemyPingComposer {
		/// <summary>Loudness with the enemy inside melee reach: the ping's own loudness
		/// identity (and the glossary demo's level).</summary>
		public const float MaxVolume = 1f;

		/// <summary>Loudness at and beyond <see cref="FloorRange"/>. Low so the swing from
		/// melee-reach full to across-the-arena faint is readable as distance, but never
		/// zero: a silent ping reads as "no enemies".</summary>
		public const float MinVolume = 0.1f;

		/// <summary>Ground distance, in world units, at and inside which the ping plays at
		/// <see cref="MaxVolume"/>: the game's default melee attack range
		/// (PlayerCombat.m_defaultAttackRange), so full volume means "in swing reach".</summary>
		public const float MeleeRange = 3f;

		/// <summary>Ground distance, in world units, at and beyond which the ping holds
		/// <see cref="MinVolume"/>.</summary>
		public const float FloorRange = 20f;

		/// <summary>
		/// The ping parameters for an enemy sitting <paramref name="right"/> world units to
		/// the camera's right (negative is left) and <paramref name="forward"/> world units
		/// toward screen-north (negative is south) of the player. Pan and pitch encode its
		/// bearing (distance-independent); volume encodes its ground distance.
		/// </summary>
		public static SoundParams Compose(float right, float forward) {
			SoundParams bearing = ProjectileSonifier.Compose(right, forward);
			float distance = (float)Math.Sqrt(right * right + forward * forward);
			return new SoundParams(bearing.Pan, bearing.Pitch, VolumeFor(distance));
		}

		/// <summary>Volume for a ground distance: <see cref="MaxVolume"/> at and inside
		/// <see cref="MeleeRange"/>, falling linearly to <see cref="MinVolume"/> at
		/// <see cref="FloorRange"/> and holding that floor beyond. A non-finite distance
		/// also yields the floor.</summary>
		public static float VolumeFor(float distance) {
			if (distance <= MeleeRange) return MaxVolume;
			// NaN fails this comparison too, so a degenerate distance drops to the floor
			// rather than blasting at full volume.
			if (!(distance < FloorRange)) return MinVolume;
			return MaxVolume + (MinVolume - MaxVolume) * ((distance - MeleeRange) / (FloorRange - MeleeRange));
		}
	}
}
