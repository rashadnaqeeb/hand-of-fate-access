namespace HandOfFateAccess.Audio {
	/// <summary>
	/// A live source of PCM, pulled on the audio thread. Two roles share the shape:
	/// the engine's own <see cref="AudioMixer"/> is the stereo source the backend
	/// hosts, and a feature that synthesizes audio in real time (the projectile
	/// tumble) implements it as a raw MONO source started with
	/// <see cref="AudioEngine.PlayLive"/> - the voice chain applies all placement
	/// (pan, delay, pitch, volume), so a live source never spatializes itself.
	///
	/// <see cref="Fill"/> runs on the audio thread: it must not allocate, block, take
	/// a lock, touch engine state, or throw. Any parameters it reads are written from
	/// the main thread without a lock, a torn read being one slightly stale block.
	/// </summary>
	public interface IPcmSource {
		/// <summary>
		/// Write exactly <paramref name="frames"/> frames of interleaved PCM for
		/// <paramref name="channels"/> channels into <paramref name="buffer"/> (which
		/// is at least frames times channels long). A source with nothing to play
		/// writes silence.
		/// </summary>
		void Fill(float[] buffer, int channels, int frames);
	}
}
