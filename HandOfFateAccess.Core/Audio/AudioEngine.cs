using System.Collections.Generic;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The non-speech audio engine: a clip registry, a pool of spatial voices, and
	/// the mixer that sums them, all in Core so every audible decision - the pan law,
	/// the interaural delay, the pitch step, the mix clamp - is
	/// unit-tested off-engine. The backend (<see cref="IAudioBackend"/>, real =
	/// FMOD) only opens the device and pulls the hosted mixer on its audio thread.
	///
	/// Sounds are referenced by a string key registered once from raw PCM, so Core
	/// stays free of engine clip types and can synthesize waveforms itself as plain
	/// float buffers. A one-shot is fired and forgotten; a continuous source (a
	/// projectile in flight, a wall drone) returns a <see cref="Voice"/> handle the
	/// caller keeps re-placing each frame with <see cref="Update"/> until
	/// <see cref="Stop"/>. A stop is click-free (the voice fades and drains its delay
	/// tail before its slot recycles), so slots free a frame or two after the call;
	/// the pool is sized well past any real concurrency.
	/// </summary>
	public static class AudioEngine {
		// Every concurrent voice draws a slot: up to 32 projectile/mover synth voices,
		// the 4 wall tones, the gambit's six, and the zone, beacon, attack, recharge,
		// locator and glossary cues on top. Sized well above that peak so a busy fight
		// never exhausts the pool.
		private const int VoiceCount = 64;

		private struct Clip {
			public float[] Mono;
			public int Rate;
		}

		private static IAudioBackend _backend;
		private static VoicePool _pool;
		private static SpatialVoice[] _voices;
		private static AudioMixer _mixer;
		private static readonly Dictionary<string, Clip> _clips = new Dictionary<string, Clip>();

		public static bool IsInitialized => _backend?.IsInitialized ?? false;
		public static bool IsAvailable => _backend?.IsAvailable ?? false;
		public static int OutputSampleRate => _backend?.OutputSampleRate ?? 0;
		public static bool IsOutputMono => _backend?.IsOutputMono ?? false;

		public static bool Initialize(IAudioBackend backend) {
			_backend = backend;
			_clips.Clear();   // a re-init (tests; a future backend swap) must not keep stale clips
			if (!_backend.Initialize()) return false;

			int rate = _backend.OutputSampleRate;
			_pool = new VoicePool(VoiceCount);
			_voices = new SpatialVoice[VoiceCount];
			for (int i = 0; i < VoiceCount; i++)
				_voices[i] = new SpatialVoice(rate);
			_mixer = new AudioMixer(_voices);
			if (!_backend.Host(_mixer)) {
				// No output means no audio: report failure so every feature disables
				// (logged) instead of playing a whole session into nothing.
				Log.Error("audio backend could not host the mixer; non-speech audio disabled");
				_pool = null;
				_voices = null;
				_mixer = null;
				return false;
			}
			return true;
		}

		// No null-guard on _backend/_pool below: these run only after Initialize
		// succeeds. A crash on misuse is preferred over silently dropping a cue
		// (invisible to a blind player). IsInitialized/IsAvailable above are readiness
		// probes, so they keep the null check.
		public static void Shutdown() {
			_backend.Shutdown();
			_clips.Clear();
			_pool = null;
			_voices = null;
			_mixer = null;
		}

		/// <summary>Once per frame: recycles voices that finished draining (freeing
		/// their slots), surfaces any voice fault the mixer parked (the audio thread
		/// cannot log), and services the backend.</summary>
		public static void Pump() {
			if (_pool != null) {
				for (int slot = 0; slot < _pool.Capacity; slot++) {
					if (!_pool.IsActiveSlot(slot) || !_voices[slot].IsFinished) continue;
					_voices[slot].Recycle();
					_pool.ReleaseSlot(slot);
				}
				string fault = _mixer.TakeFault();
				if (fault != null)
					Log.Warn("audio voice faulted and was dropped (" + fault + ")");
			}
			_backend.Pump();
		}

		/// <summary>
		/// Registers interleaved PCM under a key for later playback. Spatial sources
		/// are mono by nature; a multichannel buffer (user-supplied cue WAVs come in
		/// any layout) is folded down here by averaging its channels, since pan is
		/// applied at play time by the voice chain and a clip carries no placement
		/// of its own.
		/// </summary>
		public static void Register(string key, float[] pcm, int channels, int sampleRate) {
			if (channels < 1) {
				Log.Warn($"audio register '{key}': invalid channel count {channels}; clip dropped");
				return;
			}
			if (channels > 1) {
				int frames = pcm.Length / channels;
				var mono = new float[frames];
				for (int f = 0; f < frames; f++) {
					float sum = 0f;
					for (int c = 0; c < channels; c++) sum += pcm[f * channels + c];
					mono[f] = sum / channels;
				}
				pcm = mono;
			}
			_clips[key] = new Clip { Mono = pcm, Rate = sampleRate };
		}

		/// <summary>
		/// Plays a registered sound once, then lets its voice free itself. Fire and
		/// forget: no handle is returned, so the cue cannot be re-placed or stopped
		/// early. A cue that should track the listener while it plays uses
		/// <see cref="Play"/> with loop false and keeps the returned handle.
		/// </summary>
		public static void PlayOneShot(string key, SoundParams parameters) =>
			Play(key, parameters, false);

		/// <summary>
		/// Starts a registered sound, optionally looping, and returns a handle to its
		/// voice for live control. <see cref="Voice.None"/> if the key is unregistered
		/// (logged: a missing clip is a sound the player should have heard) or the
		/// pool was exhausted.
		/// </summary>
		public static Voice Play(string key, SoundParams parameters, bool loop) {
			if (!_clips.TryGetValue(key, out Clip clip)) {
				Log.Warn($"audio play of unregistered sound '{key}'");
				return Voice.None;
			}
			return PlayBuffer(clip.Mono, clip.Rate, loop, parameters);
		}

		/// <summary>
		/// Starts a caller-held mono buffer (rendered speech, a generated tone)
		/// without registering it: same voice chain, same handle contract as
		/// <see cref="Play"/>. The buffer is read on the audio thread until the voice
		/// ends, so the caller must not mutate it while playing.
		/// </summary>
		public static Voice PlayBuffer(float[] mono, int sampleRate, bool loop, SoundParams parameters) {
			Voice voice = _pool.Acquire();
			if (!voice.IsValid) return Voice.None;
			_voices[voice.Slot].PlayClip(mono, sampleRate, loop, parameters);
			return voice;
		}

		/// <summary>
		/// Starts a live mono source (a real-time synth), pulled on the audio thread
		/// until <see cref="Stop"/>. The source's Fill must follow
		/// <see cref="IPcmSource"/>'s audio-thread rules. The params' Pitch applies
		/// to a live source exactly as to a clip (the voice resamples the pulled
		/// stream), so the placement law's darker-means-behind cue covers synths too.
		/// </summary>
		public static Voice PlayLive(IPcmSource source, SoundParams parameters) {
			Voice voice = _pool.Acquire();
			if (!voice.IsValid) return Voice.None;
			_voices[voice.Slot].PlayLive(source, parameters);
			return voice;
		}

		/// <summary>Re-places a still-playing voice (pan, pitch, volume); the
		/// voice ramps toward the new targets click-free. A handle to a voice that has
		/// already stopped is ignored.</summary>
		public static void Update(Voice voice, SoundParams parameters) {
			if (_pool.TryResolve(voice, out int slot))
				_voices[slot].SetParams(parameters);
		}

		/// <summary>Whether a handle still points at its live voice: true from Play
		/// until the voice has fully drained (a beat after its sound ends or
		/// <see cref="Stop"/> is called). Lets a caller re-place a one-shot each frame
		/// and drop it when it finishes.</summary>
		public static bool IsPlaying(Voice voice) =>
			_pool != null && _pool.TryResolve(voice, out _);

		/// <summary>Stops a voice: it fades over a block, drains, and its slot frees
		/// on a following pump. A stale handle is ignored.</summary>
		public static void Stop(Voice voice) {
			if (_pool.TryResolve(voice, out int slot))
				_voices[slot].RequestStop();
		}

		/// <summary>
		/// Stops every voice. Reserved for global teardown (scene transitions, backend
		/// shutdown). A feature layer must not lean on this to stop its own sounds: it
		/// also kills any other feature's voices playing concurrently. Each feature
		/// keeps its own Voice handles and calls <see cref="Stop"/> per handle instead.
		/// </summary>
		public static void StopAll() {
			for (int slot = 0; slot < _pool.Capacity; slot++) {
				if (_pool.IsActiveSlot(slot))
					_voices[slot].RequestStop();
			}
		}
	}
}
