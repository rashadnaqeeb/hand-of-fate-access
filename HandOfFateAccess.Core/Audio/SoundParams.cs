namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The live spatial state of one playing sound: where it sits in the stereo
	/// field, its pitch multiplier, and its loudness. This is the medium the audio
	/// seam speaks in (as text is for speech): Core decides these numbers from game
	/// state, the spatial voice chain maps them onto what the player hears. No engine
	/// types here, so the mapping that decides what the player hears stays
	/// unit-testable.
	///
	/// Pan is the lateral fraction, -1 hard left to +1 hard right; it drives both the
	/// equal-power level pan and the interaural time difference. Pitch is a
	/// playback-rate multiplier where 1 is the clip's own pitch, clamped to two
	/// octaves each way; a positioned sound carries its fore/aft bearing here
	/// (<see cref="Spatializer.PitchFor"/>: darker means behind), while sounds with
	/// no fore/aft axis (wall tones, the gambit row, ability chimes) pass 1. Volume
	/// is linear gain, 0 to 1.
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
		/// voice chain never sees an out-of-range pan or a silent pitch. A NaN field
		/// (one frame of degenerate game geometry) becomes that field's neutral
		/// value: a NaN reaching the audio thread would poison a voice's smoothed
		/// placement permanently and, through the shared mix, everything the player
		/// hears - the invisible failure this seam exists to prevent.</summary>
		public SoundParams Clamped() => new SoundParams(
			Clamp(Pan, -1f, 1f, 0f),
			Clamp(Pitch, MinPitch, MaxPitch, 1f),
			Clamp(Volume, 0f, 1f, 0f));

		private static float Clamp(float v, float lo, float hi, float nan) =>
			float.IsNaN(v) ? nan : (v < lo ? lo : (v > hi ? hi : v));
	}
}
