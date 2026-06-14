namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The engine seam for non-speech audio: a pool of independently controllable
	/// voices over which Core plays spatialized cues. The real implementation
	/// (FmodAudioBackend) wraps an FMOD Core System; tests use a fake.
	///
	/// Sounds are referenced by a string key registered once from raw PCM, so Core
	/// stays free of engine clip types and can synthesize waveforms itself (the
	/// gambit tones, the combat samples) as plain float buffers. A one-shot is fired
	/// and forgotten; a continuous source (a projectile in flight, a wall drone)
	/// returns a <see cref="Voice"/> handle the caller keeps controlling each frame
	/// with <see cref="Update"/> until <see cref="Stop"/>.
	/// </summary>
	public interface IAudioBackend {
		bool IsInitialized { get; }
		bool IsAvailable { get; }

		/// <summary>The backend's mixer sample rate. A synth source should generate at this rate so
		/// the backend resamples nothing and the output matches what a device-rate synth produced.
		/// Zero before <see cref="Initialize"/> succeeds.</summary>
		int OutputSampleRate { get; }

		bool Initialize();
		void Shutdown();

		/// <summary>
		/// Services the backend once per frame from the update pump. FMOD's System::update
		/// (mixer housekeeping, voice management) runs here; a backend that mixes on its own
		/// thread and needs no per-frame servicing leaves it empty.
		/// </summary>
		void Pump();

		/// <summary>
		/// Registers interleaved PCM under a key for later playback. <paramref name="pcm"/>
		/// length must be channels times the per-channel frame count. Mono is the norm
		/// for spatial sources, since pan is applied at play time.
		/// </summary>
		void Register(string key, float[] pcm, int channels, int sampleRate);

		/// <summary>
		/// Registers a live synthesized source under a key, the real-time counterpart to
		/// <see cref="Register"/>'s fixed PCM. The backend pulls <paramref name="source"/> on its
		/// audio thread to feed the voice; the source fills interleaved PCM including its own pan
		/// and volume. Play it with <see cref="Play"/> by key, looping for a continuous voice.
		/// </summary>
		void RegisterSynth(string key, IPcmSource source, int channels, int sampleRate);

		/// <summary>
		/// Plays a registered sound once, then lets its voice free itself. Fire and forget:
		/// no handle is returned, so the cue cannot be stopped early. A cue that may need to
		/// be cut short (a windup beep the attack outruns) should instead use
		/// <see cref="Play"/> with loop false and keep the returned handle.
		/// </summary>
		void PlayOneShot(string key, SoundParams parameters);

		/// <summary>
		/// Starts a registered sound, optionally looping, and returns a handle to its
		/// voice for live control. <see cref="Voice.None"/> if the pool was exhausted.
		/// </summary>
		Voice Play(string key, SoundParams parameters, bool loop);

		/// <summary>Re-applies pan/pitch/volume to a still-playing voice. A handle to a
		/// voice that has already stopped is ignored.</summary>
		void Update(Voice voice, SoundParams parameters);

		/// <summary>Stops a voice and frees it. A stale handle is ignored.</summary>
		void Stop(Voice voice);

		/// <summary>
		/// Stops every voice. Reserved for global teardown (scene transitions, backend
		/// shutdown). A feature layer must not lean on this to stop its own sounds: it also
		/// kills any other feature's voices playing concurrently. Each feature keeps its own
		/// Voice handles and calls <see cref="Stop"/> per handle instead.
		/// </summary>
		void StopAll();
	}
}
