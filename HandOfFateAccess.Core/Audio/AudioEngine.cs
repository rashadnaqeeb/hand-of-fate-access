namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Static facade delegating to an IAudioBackend instance, mirroring SpeechEngine.
	/// The audio features (combat sonification, the chance gambit) drive their voices
	/// through here so they never hold the backend directly.
	/// </summary>
	public static class AudioEngine {
		private static IAudioBackend _backend;

		public static bool IsInitialized => _backend?.IsInitialized ?? false;
		public static bool IsAvailable => _backend?.IsAvailable ?? false;
		public static int OutputSampleRate => _backend?.OutputSampleRate ?? 0;
		public static bool IsOutputMono => _backend?.IsOutputMono ?? false;

		public static bool Initialize(IAudioBackend backend) {
			_backend = backend;
			return _backend.Initialize();
		}

		// No null-guard on _backend below: these run only after Initialize succeeds.
		// A crash on misuse is preferred over silently dropping a cue (invisible to a
		// blind player). IsInitialized/IsAvailable above are readiness probes, so they
		// keep the null check.
		public static void Shutdown() => _backend.Shutdown();

		public static void Pump() => _backend.Pump();

		public static void Register(string key, float[] pcm, int channels, int sampleRate) =>
			_backend.Register(key, pcm, channels, sampleRate);

		public static void RegisterSynth(string key, IPcmSource source, int channels, int sampleRate) =>
			_backend.RegisterSynth(key, source, channels, sampleRate);

		public static void PlayOneShot(string key, SoundParams parameters) =>
			_backend.PlayOneShot(key, parameters);

		public static Voice Play(string key, SoundParams parameters, bool loop) =>
			_backend.Play(key, parameters, loop);

		public static void Update(Voice voice, SoundParams parameters) =>
			_backend.Update(voice, parameters);

		public static void Stop(Voice voice) => _backend.Stop(voice);

		public static void StopAll() => _backend.StopAll();
	}
}
