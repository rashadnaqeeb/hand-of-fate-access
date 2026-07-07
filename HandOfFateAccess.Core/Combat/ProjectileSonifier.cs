using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Turns a projectile's position relative to the player, measured on the combat
	/// camera's ground plane, into the placement the player hears for it. This is the
	/// "what the player hears" decision for projectile sonification, so it lives in
	/// Core and is unit-tested off-engine; the plugin adapter only reads each
	/// projectile's offset off its live transform and feeds the two numbers in.
	///
	/// Bearing rides the shared placement law (<see cref="Spatializer"/>): east/west
	/// (camera-right) is the stereo position, level and interaural delay together;
	/// fore/aft is the down-biased pitch, the authored tumble due north darkening to
	/// <see cref="Spatializer.DownOctaves"/> lower due south, so darker means behind
	/// and bright means ahead. The reflected shot's faster flutter is synthesized
	/// identity and reads through the placement shift.
	///
	/// Distance is carried by volume alone, loudest when the projectile is on top of
	/// the player and tapering to a faint floor far off: a closing projectile swells
	/// while a distant one stays just audible. It never falls fully silent, because
	/// as long as it flies it is a live threat the player should be able to track.
	/// </summary>
	public static class ProjectileSonifier {
		/// <summary>Distance, in world units, over which volume falls from its peak to the
		/// far floor. Roughly the reach a projectile crosses in an arena.</summary>
		public const float FalloffRange = 16f;

		/// <summary>Loudness when the projectile is on top of the player.</summary>
		public const float MaxVolume = 0.8f;

		/// <summary>Loudness at and beyond <see cref="FalloffRange"/>: a faint floor, not
		/// silence, so an active projectile is always at least audible.</summary>
		public const float MinVolume = 0.2f;

		/// <summary>
		/// The voice parameters for a projectile sitting <paramref name="right"/> world
		/// units to the camera's right (negative is left) and <paramref name="forward"/>
		/// world units toward screen-north (negative is south) of the player. Pan and
		/// pitch encode its bearing (pure bearing past the near-field crossover); volume
		/// encodes its ground distance.
		/// </summary>
		public static SoundParams Compose(float right, float forward) {
			float distance = (float)Math.Sqrt(right * right + forward * forward);
			return Spatializer.Place(right, forward, VolumeFor(distance));
		}

		/// <summary>Volume for a ground distance: <see cref="MaxVolume"/> at zero, falling
		/// linearly to <see cref="MinVolume"/> at <see cref="FalloffRange"/> and holding
		/// that floor beyond. A non-finite distance also yields the floor.</summary>
		public static float VolumeFor(float distance) {
			if (distance <= 0f) return MaxVolume;
			// NaN fails this comparison too, so a degenerate distance drops to the floor
			// rather than blasting at full volume.
			if (!(distance < FalloffRange)) return MinVolume;
			return MaxVolume + (MinVolume - MaxVolume) * (distance / FalloffRange);
		}
	}
}
