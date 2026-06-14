namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The live spatial state of one playing sound: where it sits in the stereo
	/// field, its pitch multiplier, and its loudness. This is the medium the audio
	/// seam speaks in (as text is for speech): Core decides these three numbers from
	/// game state, the backend maps them onto a playing voice. No engine types here,
	/// so the mapping that decides what the player hears stays unit-testable.
	///
	/// Pan is the equal-power stereo position, -1 hard left to +1 hard right. Pitch
	/// is a playback-rate multiplier where 1 is the clip's own pitch (the backend voice's
	/// pitch); it is clamped to two octaves each way. Volume is linear
	/// gain, 0 to 1.
	///
	/// Use the constructor or <see cref="Neutral"/>. A default(SoundParams) has
	/// Pitch 0, which is not a valid neutral value; <see cref="Clamped"/> floors it
	/// to MinPitch so a misuse degrades audibly rather than into silence.
	/// </summary>
	public readonly struct SoundParams {
		public const float MinPitch = 0.25f;
		public const float MaxPitch = 4.0f;

		/// <summary>Centered, unmodified pitch, full volume.</summary>
		public static readonly SoundParams Neutral = new SoundParams(0f, 1f, 1f);

		public readonly float Pan;
		public readonly float Pitch;
		public readonly float Volume;

		public SoundParams(float pan, float pitch, float volume) {
			Pan = pan;
			Pitch = pitch;
			Volume = volume;
		}

		/// <summary>This value with each field forced into its valid range, so the
		/// backend never hands a voice an out-of-range pan or a silent pitch.</summary>
		public SoundParams Clamped() => new SoundParams(
			Clamp(Pan, -1f, 1f),
			Clamp(Pitch, MinPitch, MaxPitch),
			Clamp(Volume, 0f, 1f));

		private static float Clamp(float v, float lo, float hi) =>
			v < lo ? lo : (v > hi ? hi : v);
	}
}
