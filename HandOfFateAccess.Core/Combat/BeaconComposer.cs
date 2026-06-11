using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears for a level object worth walking to: the chest, the
	/// exit, and the court fights' end circle (which borrows the exit's voice - standing in
	/// it is how a boss fight ends), the walk-in objects a sighted player simply sees
	/// standing in the arena. Each pings on a fixed cadence with a sample naming what it is,
	/// positioned by the shared bearing grammar (pan = east/west, the down-biased pitch =
	/// north/south, volume = distance), so the skill of reading a projectile or telegraph
	/// transfers directly and "walk toward the sound" is the whole instruction.
	///
	/// Pings, not loops, deliberately: in the established language a continuous loop means
	/// danger (zones, traps) and the wind means walls, so a friendly destination must not
	/// borrow the hazard grammar; the enemy locator has already set "ping = point of
	/// interest". The gap of silence between pings is the distance readout, parking-sensor
	/// style: sparse when the object is far, tightening as the player closes in, so an
	/// approach is audible as an accelerating rhythm. Volume alone could not say this - it
	/// floors rather than fading out, and across a small room it barely moves, which in
	/// play made a far exit sound like a companion at fixed distance ("following me
	/// around"). The gap then starts when the sound ENDS, so the cadence also follows the
	/// clip's length at the pitch it played (a pitched-down ping runs longer and repings
	/// later). The exit's first ping is staggered behind the chest's at level entry.
	///
	/// Unlike every hazard voice, volume floors instead of fading to silence: a beacon is a
	/// navigation target, so silence must mean "no chest here", never "chest far away". The
	/// one suppression is standing on the object itself (the walk-in trigger is about to
	/// fire, or has and the open animation is playing): a full-volume ping on top of the
	/// player's head says nothing and masks the fight.
	/// </summary>
	public static class BeaconComposer {
		/// <summary>Sample key (and sounds-folder file stem) for the chest beacon.</summary>
		public const string ChestKey = "beacon_chest";

		/// <summary>Sample key (and sounds-folder file stem) for the level exit beacon.</summary>
		public const string ExitKey = "beacon_exit";

		/// <summary>Re-check interval when nothing pinged (no live object of the kind, or the
		/// player standing on it): how soon a beacon resumes once one exists again.</summary>
		public const float PingGap = 0.5f;

		/// <summary>Seconds of silence between pings with the object in reach: the eager
		/// near-cadence the gap tightens to as the player closes in.</summary>
		public const float NearPingGap = 0.3f;

		/// <summary>Seconds of silence between pings at <see cref="RangingCurve.FloorRange"/>
		/// and beyond: present, but clearly far.</summary>
		public const float FarPingGap = 3f;

		/// <summary>The exit's first ping waits this long behind the chest's at level entry,
		/// so in a level with both (a trap room before its chest is taken) the opening pings
		/// land one after the other instead of stacked. Only the first cycle needs the help:
		/// the differing clip lengths drift the cadences apart from then on.</summary>
		public const float ExitStagger = 1f;

		/// <summary>Loudness when the object is within reach. Guidance, not an alert: it
		/// sits under the telegraph cues.</summary>
		public const float MaxVolume = 0.7f;

		/// <summary>Loudness at and beyond <see cref="RangingCurve.FloorRange"/>: the
		/// locator's faint floor, not silence, so an existing beacon is always audible
		/// somewhere in the field.</summary>
		public const float MinVolume = 0.1f;

		/// <summary>Ground distance, in world units, within which the ping is suppressed:
		/// the player is standing on the object.</summary>
		public const float ReachedRange = 1.5f;

		/// <summary>
		/// The voice parameters for a beacon object sitting <paramref name="right"/> world
		/// units to the camera's right (negative is left) and <paramref name="forward"/>
		/// toward screen-north (negative is south) of the player, plus the ground
		/// <paramref name="distance"/> the cadence is scheduled from. False, with no
		/// parameters, when the player has effectively reached the object and the ping is
		/// suppressed.
		/// </summary>
		public static bool TryCompose(float right, float forward, out SoundParams parameters, out float distance) {
			distance = (float)Math.Sqrt(right * right + forward * forward);
			if (distance <= ReachedRange) {
				parameters = default(SoundParams);
				return false;
			}
			float volume = VolumeFor(distance);

			// L1-normalize the horizontal direction so pan and pitch share one unit of
			// deflection by angle, the grammar shared with every other combat sound.
			float budget = Math.Abs(right) + Math.Abs(forward);
			float pan = right / budget;
			float deflection = forward / budget;
			parameters = new SoundParams(pan, ProjectileSonifier.PitchFor(deflection), volume);
			return true;
		}

		/// <summary>
		/// When a ping played at <paramref name="now"/> should ping again:
		/// <see cref="GapFor"/> the object's <paramref name="distance"/> in silence after
		/// the sound ends. The clip's authored <paramref name="clipDuration"/> stretches by
		/// the played <paramref name="pitch"/> (a playback-rate multiplier, so pitched-down
		/// runs longer), keeping the gap a real gap at any bearing.
		/// </summary>
		public static float NextPingTime(float now, float clipDuration, float pitch, float distance) =>
			now + clipDuration / pitch + GapFor(distance);

		/// <summary>The silence between pings for a ground distance: <see cref="NearPingGap"/>
		/// in reach, stretching to <see cref="FarPingGap"/> at
		/// <see cref="RangingCurve.FloorRange"/> and holding there beyond, on the shared
		/// ranging curve (non-finite reads as far). The distance readout the volume floor
		/// cannot give.</summary>
		public static float GapFor(float distance) =>
			FarPingGap + (NearPingGap - FarPingGap) * RangingCurve.Closeness(distance);

		/// <summary>Volume for a ground distance: <see cref="MaxVolume"/> within reach,
		/// falling to <see cref="MinVolume"/> at <see cref="RangingCurve.FloorRange"/> and
		/// holding that floor beyond, on the shared ranging curve (non-finite reads as
		/// far), so beacon loudness means the same distance as every other ranged
		/// sound.</summary>
		public static float VolumeFor(float distance) =>
			MinVolume + (MaxVolume - MinVolume) * RangingCurve.Closeness(distance);
	}
}
