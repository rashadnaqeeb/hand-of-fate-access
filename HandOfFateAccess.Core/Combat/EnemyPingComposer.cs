using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears for an enemy-locator ping: the bearing comes from the
	/// projectile grammar (<see cref="ProjectileSonifier"/>: pan = east/west, down-biased
	/// pitch = north/south), so the learned spatial language transfers, but the loudness
	/// comes from the telegraph alert policy (<see cref="AttackCueComposer.VolumeFor"/>:
	/// a high far floor, never a faint one). The distinction is the codebase's own: a
	/// projectile voice is ambient tracking the player listens to continuously, so it may
	/// fade with distance, but the ping is a one-shot answer to an explicit question, and
	/// a missed answer reads as "no enemies" - so even an enemy across the arena must
	/// answer audibly.
	/// </summary>
	public static class EnemyPingComposer {
		/// <summary>Loudness with the enemy on top of the player: the alert policy's
		/// peak, the ping's own loudness identity (and the glossary demo's level).</summary>
		public const float MaxVolume = AttackCueComposer.MaxVolume;

		/// <summary>
		/// The ping parameters for an enemy sitting <paramref name="right"/> world units to
		/// the camera's right (negative is left) and <paramref name="forward"/> world units
		/// toward screen-north (negative is south) of the player. Pan and pitch encode its
		/// bearing (distance-independent); volume encodes its ground distance on the alert
		/// floor.
		/// </summary>
		public static SoundParams Compose(float right, float forward) {
			SoundParams bearing = ProjectileSonifier.Compose(right, forward);
			float distance = (float)Math.Sqrt(right * right + forward * forward);
			return new SoundParams(bearing.Pan, bearing.Pitch, AttackCueComposer.VolumeFor(distance));
		}
	}
}
