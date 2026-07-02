namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The engine seam for non-speech audio output. Everything audible - the clip
	/// registry, the voice pool, spatialization, mixing - lives in Core
	/// (<see cref="AudioEngine"/>); the backend's whole job is the device: open an
	/// output, pull the hosted mixer on its audio thread, report its format. The real
	/// implementation (FmodAudioBackend) hosts the mixer as one FMOD generator DSP;
	/// tests use a fake that pulls the mixer directly.
	/// </summary>
	public interface IAudioBackend {
		bool IsInitialized { get; }
		bool IsAvailable { get; }

		/// <summary>The backend's mixer sample rate. Core generates and spatializes at
		/// this rate so the backend resamples nothing. Zero before
		/// <see cref="Initialize"/> succeeds.</summary>
		int OutputSampleRate { get; }

		/// <summary>Whether the output is a single speaker. Every spatial cue conveys
		/// position by stereo placement, so on mono output that information is lost;
		/// the plugin warns once. False before <see cref="Initialize"/> succeeds.</summary>
		bool IsOutputMono { get; }

		bool Initialize();
		void Shutdown();

		/// <summary>
		/// Services the backend once per frame from the update pump. FMOD's
		/// System::update (housekeeping, fault relay) runs here; a backend that needs
		/// no per-frame servicing leaves it empty.
		/// </summary>
		void Pump();

		/// <summary>
		/// Starts pulling <paramref name="mixer"/> on the audio thread for the rest of
		/// the session: the one stereo output every mod sound is summed into. The
		/// mixer emits silence when nothing plays, so the backend never starts or
		/// stops it per sound. False (logged, and <see cref="IsAvailable"/> drops)
		/// when the output could not be set up: a backend with no output has no
		/// audio, and every feature must see that rather than play into nothing.
		/// </summary>
		bool Host(IPcmSource mixer);
	}
}
