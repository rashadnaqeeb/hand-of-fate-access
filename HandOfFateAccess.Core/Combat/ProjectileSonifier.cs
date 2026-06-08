using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Turns a projectile's position relative to the player, measured on the combat
	/// camera's ground plane, into the pan, pitch, and volume the player hears for it.
	/// This is the "what the player hears" decision for projectile sonification, so it
	/// lives in Core and is unit-tested off-engine; the plugin adapter only reads each
	/// projectile's offset off its live transform and feeds the two numbers in.
	///
	/// Pan and pitch carry the projectile's bearing, one screen axis each:
	///   east/west (camera-right) maps to stereo pan, full left to full right.
	///   north/south (camera-forward, into the screen) maps to pitch, brightest (the
	///   authored sample, unshifted) due north and darkening toward the south by up to
	///   <see cref="DownOctaves"/>.
	/// The pitch is biased entirely downward, never up: the sample is heard pure at due
	/// north and only ever pitched down elsewhere. Up-shifting a sample means stretching
	/// time to hold tempo, which smears the tumble's transients into a metallic buzz;
	/// down-shifting compresses instead, which stays clean, so the live shifter only ever
	/// works in that clean direction.
	///
	/// The horizontal direction is L1-normalized, so the two axes split one unit of
	/// deflection between them by angle rather than each saturating on its own: a
	/// projectile due west is full-left, one due north is centred and brightest, and one
	/// to the northwest is half-left and a quarter of the way darkened. Bearing is
	/// distance-independent: a due-west projectile is hard left whether it sits next to the
	/// player or across the arena.
	///
	/// Distance is carried by volume alone, loudest when the projectile is on top of the
	/// player and tapering to a faint floor far off: a closing projectile swells while a
	/// distant one stays just audible. It never falls fully silent, because as long as it
	/// flies it is a live threat the player should be able to track.
	/// </summary>
	public static class ProjectileSonifier {
		/// <summary>Octaves the pitch is dropped at full south deflection (due north is
		/// unshifted). The whole north/south span, so the cue runs from the authored sample
		/// down to <see cref="DownOctaves"/> lower (0.8 octave ~ 0.57x). Tunable by ear.</summary>
		public const float DownOctaves = 0.8f;

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
		/// world units toward screen-north (negative is south) of the player. Pan and pitch
		/// encode its bearing (distance-independent); volume encodes its ground distance.
		/// </summary>
		public static SoundParams Compose(float right, float forward) {
			float distance = (float)Math.Sqrt(right * right + forward * forward);
			float volume = VolumeFor(distance);

			// L1-normalize the horizontal direction so pan and pitch share one unit of
			// deflection by angle: due west (-1, 0), due north (0, +1), northwest (-0.5, +0.5).
			float budget = Math.Abs(right) + Math.Abs(forward);
			if (budget < 1e-6f) return new SoundParams(0f, PitchFor(0f), volume);  // on the player: no bearing, mid pitch

			float pan = right / budget;
			float deflection = forward / budget;
			return new SoundParams(pan, PitchFor(deflection), volume);
		}

		/// <summary>
		/// The down-biased pitch ratio for a north/south <paramref name="deflection"/> in
		/// [-1, +1] (south to north): 1 (the authored sample) at due north, falling to
		/// <see cref="DownOctaves"/> lower at due south, and never above 1. Mid-deflection
		/// (due east or west) sits halfway down.
		/// </summary>
		public static float PitchFor(float deflection) {
			float octaves = (deflection - 1f) * 0.5f * DownOctaves;  // 0 at north, -DownOctaves at south
			return (float)Math.Pow(2.0, octaves);
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
