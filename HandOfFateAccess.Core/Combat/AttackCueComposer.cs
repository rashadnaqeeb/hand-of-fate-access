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
	/// Pan and pitch carry the attacker's bearing on the combat camera's ground plane, the same
	/// grammar as projectile sonification so the player learns one spatial language for the whole
	/// fight: east/west (camera-right) maps to stereo pan, north/south (into the screen) to a
	/// down-biased pitch, brightest due north and darkening south. Volume carries distance, but a
	/// telegraph is a critical alert rather than ambient tracking, so it stays loud throughout and
	/// only tapers to a high floor far off, never to a faint one.
	/// </summary>
	public static class AttackCueComposer {
		/// <summary>Sample key for a blockable attack: block it, or reflect it if ranged.</summary>
		public const string BlockKey = "attack_block";

		/// <summary>Sample key for an unblockable attack: dodge, never block.</summary>
		public const string DodgeKey = "attack_dodge";

		/// <summary>Octaves the pitch is dropped at full south deflection (due north is
		/// unshifted), matching the projectile grammar. Down only: up-shifting a sample
		/// stretches time and smears it.</summary>
		public const float DownOctaves = 0.8f;

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

		/// <summary>
		/// The voice parameters for an attacker sitting <paramref name="right"/> world units to
		/// the camera's right (negative is left) and <paramref name="forward"/> world units
		/// toward screen-north (negative is south) of the player. Pan and pitch encode its
		/// bearing (distance-independent); volume encodes its ground distance.
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
		/// <see cref="DownOctaves"/> lower at due south, never above 1. Mid-deflection (due
		/// east or west) sits halfway down.
		/// </summary>
		public static float PitchFor(float deflection) {
			float octaves = (deflection - 1f) * 0.5f * DownOctaves;  // 0 at north, -DownOctaves at south
			return (float)Math.Pow(2.0, octaves);
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
