namespace HandOfFateAccess.Audio {
	/// <summary>
	/// A handle to one acquired voice in a <see cref="VoicePool"/>. Carries the slot
	/// plus the generation that slot held at acquire time, so a handle to a voice
	/// that has since stopped and been recycled no longer resolves: updating or
	/// stopping a finished sound becomes a no-op instead of silently hijacking
	/// whatever sound now owns the slot. Caller-held handles are opaque; only the
	/// pool reads the fields.
	/// </summary>
	public readonly struct Voice {
		/// <summary>The handle returned when no voice was free. <see cref="IsValid"/> is false.</summary>
		public static readonly Voice None = new Voice(-1, 0);

		public readonly int Slot;
		public readonly int Generation;

		public Voice(int slot, int generation) {
			Slot = slot;
			Generation = generation;
		}

		public bool IsValid => Slot >= 0;
	}
}
