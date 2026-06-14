namespace HandOfFateAccess.Audio {
	/// <summary>
	/// A live source of synthesized PCM, pulled on the backend's audio thread. A feature that
	/// generates its audio in real time (the projectile tumble, the gambit tones) implements
	/// this and registers it with <see cref="IAudioBackend.RegisterSynth"/>; the backend then
	/// calls <see cref="Fill"/> from its mixer to feed a voice, so the feature never owns an
	/// engine audio object of its own. The source applies its own pan and volume into the
	/// interleaved output, so the backend plays the voice without further spatialization.
	///
	/// <see cref="Fill"/> runs on the audio thread: it must not allocate, block, take a lock,
	/// touch engine state, or throw. Any parameters it reads (pan, pitch) are written from the
	/// main thread without a lock, a torn read being one slightly stale block.
	/// </summary>
	public interface IPcmSource {
		/// <summary>
		/// Write exactly <paramref name="frames"/> frames of interleaved PCM for
		/// <paramref name="channels"/> channels into <paramref name="buffer"/> (which is at
		/// least frames times channels long), including any pan and volume the source applies.
		/// A source with nothing to play writes silence.
		/// </summary>
		void Fill(float[] buffer, int channels, int frames);
	}
}
