using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears for a level object worth walking to: the chest and the
	/// exit, the two walk-in objects a sighted player simply sees standing in the arena. Each
	/// pings on a fixed cadence with a sample naming what it is, positioned by the shared
	/// bearing grammar (pan = east/west, the down-biased pitch = north/south, volume =
	/// distance), so the skill of reading a projectile or telegraph transfers directly and
	/// "walk toward the sound" is the whole instruction.
	///
	/// Pings, not loops, deliberately: in the established language a continuous loop means
	/// danger (zones, traps) and the wind means walls, so a friendly destination must not
	/// borrow the hazard grammar; the enemy locator has already set "ping = point of
	/// interest". Each beacon repings a fixed gap of silence after its sound ends, so the
	/// cadence follows the clip's length at the pitch it played (a pitched-down ping runs
	/// longer and so repings later). The exit's first ping is staggered behind the chest's;
	/// afterwards the differing clip lengths keep the two drifting apart on their own.
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

		/// <summary>Seconds of silence between one ping ending and the next beginning.</summary>
		public const float PingGap = 0.5f;

		/// <summary>The exit's first ping waits this long behind the chest's at level entry,
		/// so in a level with both (a trap room before its chest is taken) the opening pings
		/// land one after the other instead of stacked. Only the first cycle needs the help:
		/// the differing clip lengths drift the cadences apart from then on.</summary>
		public const float ExitStagger = 1f;

		/// <summary>Distance, in world units, over which volume falls from its peak to the
		/// far floor. Longer than the hazard ranges: a beacon is navigated to from anywhere
		/// in the level, not reacted to up close.</summary>
		public const float FalloffRange = 30f;

		/// <summary>Loudness when the object is close by. Guidance, not an alert: it sits
		/// under the telegraph cues.</summary>
		public const float MaxVolume = 0.7f;

		/// <summary>Loudness at and beyond <see cref="FalloffRange"/>: a faint floor, not
		/// silence, so an existing beacon is always audible somewhere in the field.</summary>
		public const float MinVolume = 0.25f;

		/// <summary>Ground distance, in world units, within which the ping is suppressed:
		/// the player is standing on the object.</summary>
		public const float ReachedRange = 1.5f;

		/// <summary>
		/// The voice parameters for a beacon object sitting <paramref name="right"/> world
		/// units to the camera's right (negative is left) and <paramref name="forward"/>
		/// toward screen-north (negative is south) of the player. False, with no parameters,
		/// when the player has effectively reached the object and the ping is suppressed.
		/// </summary>
		public static bool TryCompose(float right, float forward, out SoundParams parameters) {
			float distance = (float)Math.Sqrt(right * right + forward * forward);
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
		/// When a ping played at <paramref name="now"/> should ping again: <see cref="PingGap"/>
		/// of silence after the sound ends. The clip's authored <paramref name="clipDuration"/>
		/// stretches by the played <paramref name="pitch"/> (a playback-rate multiplier, so
		/// pitched-down runs longer), keeping the gap a real gap at any bearing.
		/// </summary>
		public static float NextPingTime(float now, float clipDuration, float pitch) =>
			now + clipDuration / pitch + PingGap;

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
