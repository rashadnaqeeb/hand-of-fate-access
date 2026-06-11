using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides what the player hears when an equipped ability finishes recharging: one
	/// authored chime, panned hard to the ability's side. The game lays the two ability
	/// buttons out by side (weapon on the left bumper, artifact on the right), so the pan
	/// IS the slot identity: a chime in the left ear means the weapon button works again,
	/// in the right ear the artifact button. Nothing else is modulated; the cue is a
	/// status note pinned to its button, not a positional voice.
	/// </summary>
	public static class RechargeCueComposer {
		/// <summary>Sample key for the recharge chime; one clip serves both slots.</summary>
		public const string Key = "ability_recharge";

		/// <summary>Loudness for both slots: under the reaction-critical attack telegraphs,
		/// over the ambient tracking floor. A missed chime costs an opportunity, not a hit.</summary>
		public const float Volume = 0.8f;

		/// <summary>The weapon slot's voice: hard left, like its bumper.</summary>
		public static SoundParams Weapon => new SoundParams(-1f, 1f, Volume);

		/// <summary>The artifact slot's voice: hard right, like its bumper.</summary>
		public static SoundParams Artifact => new SoundParams(1f, 1f, Volume);
	}
}
