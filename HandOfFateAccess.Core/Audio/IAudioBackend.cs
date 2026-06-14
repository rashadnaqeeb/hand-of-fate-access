namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The engine seam for non-speech audio: a pool of independently controllable
	/// voices over which Core plays spatialized cues. The real implementation
	/// (UnityAudioBackend) wraps a set of AudioSources; tests use a fake.
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
		bool Initialize();
		void Shutdown();

		/// <summary>
		/// Services the backend once per frame from the update pump. Backends that mix on
		/// their own thread and need no per-frame servicing (the Unity AudioSource pool)
		/// leave this empty; a backend wrapping a native engine that requires a periodic
		/// update call (FMOD's System::update) does its housekeeping here.
		/// </summary>
		void Pump();

		/// <summary>
		/// Registers interleaved PCM under a key for later playback. <paramref name="pcm"/>
		/// length must be channels times the per-channel frame count. Mono is the norm
		/// for spatial sources, since pan is applied at play time.
		/// </summary>
		void Register(string key, float[] pcm, int channels, int sampleRate);

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
