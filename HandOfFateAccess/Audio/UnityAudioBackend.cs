using System;
using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// IAudioBackend over a fixed pool of Unity AudioSources. One persistent
	/// GameObject carries <see cref="VoiceCount"/> AudioSources, each a 2D voice
	/// (spatialBlend 0) whose pan/pitch/volume we drive directly from SoundParams
	/// rather than through Unity's 3D rolloff, so the spatial grammar Core computes
	/// is the one the player hears. A separate AudioSource per voice is the whole
	/// point: the reference mod funneled every cue through one shared source, which
	/// cut off concurrent sounds.
	///
	/// Slot bookkeeping lives in the engine-agnostic <see cref="VoicePool"/>; this
	/// class only binds slot indices to AudioSources and reclaims slots whose
	/// one-shot has finished. Clips are built once from registered PCM via
	/// AudioClip.Create (Unity 5.3's audio engine is FMOD, so no external library is
	/// involved). Created from the update pump once the engine is live, never from
	/// Awake.
	/// </summary>
	public sealed class UnityAudioBackend : IAudioBackend {
		// The four wall tones plus a few transient one-shots (the collision cue). Projectiles
		// no longer draw from here: they use their own PitchVoicePool of pitch-shift voices.
		private const int VoiceCount = 24;

		private GameObject _root;
		private AudioSource[] _sources;
		private VoicePool _pool;
		private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
		private bool _initialized;
		private bool _available;

		public bool IsInitialized => _initialized;
		public bool IsAvailable => _available;

		public bool Initialize() {
			if (_initialized) return _available;

			try {
				_root = new GameObject("HoFAccessAudio");
				ScenePersistence.Protect(_root);
				_sources = new AudioSource[VoiceCount];
				for (int i = 0; i < VoiceCount; i++) {
					AudioSource src = _root.AddComponent<AudioSource>();
					src.playOnAwake = false;
					src.spatialBlend = 0f;
					src.bypassReverbZones = true;
					_sources[i] = src;
				}
				_pool = new VoicePool(VoiceCount);
				_available = true;
				_initialized = true;
				Log.Info($"audio backend initialized with {VoiceCount} voices");
				return true;
			} catch (Exception ex) {
				Log.Error($"audio backend init failed: {ex}");
				_initialized = true;
				_available = false;
				return false;
			}
		}

		// Unity services its own audio mixer; the AudioSource pool needs no per-frame call.
		public void Pump() { }

		public void Shutdown() {
			if (!_initialized) return;

			try {
				StopAll();
				// Clips are standalone native assets held only here; destroying the root
				// frees the AudioSources but not these, so release them explicitly.
				foreach (AudioClip clip in _clips.Values)
					UnityEngine.Object.Destroy(clip);
				_clips.Clear();
				if (_root != null)
					UnityEngine.Object.Destroy(_root);
				Log.Info("audio backend shutdown");
			} catch (Exception ex) {
				Log.Warn($"audio backend shutdown error: {ex}");
			} finally {
				_initialized = false;
				_available = false;
			}
		}

		public void Register(string key, float[] pcm, int channels, int sampleRate) {
			if (!_available) return;

			try {
				if (pcm.Length % channels != 0)
					Log.Warn($"audio register '{key}': PCM length {pcm.Length} is not a multiple of {channels} channels; trailing samples dropped");
				int frames = pcm.Length / channels;
				AudioClip clip = AudioClip.Create(key, frames, channels, sampleRate, false);
				clip.SetData(pcm, 0);
				// Destroy a clip this key already held: AudioClip is a native-backed
				// UnityEngine.Object the GC never collects, so overwriting the entry
				// without destroying the old one leaks its native memory.
				if (_clips.TryGetValue(key, out AudioClip existing))
					UnityEngine.Object.Destroy(existing);
				_clips[key] = clip;
			} catch (Exception ex) {
				Log.Error($"audio register failed for '{key}': {ex}");
			}
		}

		public void PlayOneShot(string key, SoundParams parameters) {
			Play(key, parameters, false);
		}

		public Voice Play(string key, SoundParams parameters, bool loop) {
			if (!_available) return Voice.None;

			AudioClip clip;
			if (!_clips.TryGetValue(key, out clip)) {
				Log.Warn($"audio play of unregistered sound '{key}'");
				return Voice.None;
			}

			ReclaimFinished();
			Voice voice = _pool.Acquire();
			if (!voice.IsValid) return Voice.None;

			try {
				AudioSource src = _sources[voice.Slot];
				src.clip = clip;
				src.loop = loop;
				Apply(src, parameters.Clamped());
				src.Play();
				return voice;
			} catch (Exception ex) {
				Log.Error($"audio play failed for '{key}': {ex}");
				_pool.Release(voice);
				return Voice.None;
			}
		}

		public void Update(Voice voice, SoundParams parameters) {
			if (!_available) return;
			if (!_pool.TryResolve(voice, out int slot)) return;
			Apply(_sources[slot], parameters.Clamped());
		}

		public void Stop(Voice voice) {
			if (!_available) return;
			if (!_pool.TryResolve(voice, out int slot)) return;
			_sources[slot].Stop();
			_pool.Release(voice);
		}

		public void StopAll() {
			if (!_available) return;
			for (int slot = 0; slot < _pool.Capacity; slot++) {
				if (!_pool.IsActiveSlot(slot)) continue;
				_sources[slot].Stop();
				_pool.ReleaseSlot(slot);
			}
		}

		// A non-looping voice plays to its end with no Stop call, so before each Acquire
		// reclaim any slot whose one-shot has finished. Looping voices are excluded by the
		// !loop guard, not just by isPlaying: a loop reports isPlaying false while globally
		// paused (AudioListener.pause), and reclaiming it then would invalidate a handle the
		// caller is still driving and hand its slot to another sound.
		private void ReclaimFinished() {
			for (int slot = 0; slot < _pool.Capacity; slot++) {
				AudioSource src = _sources[slot];
				if (_pool.IsActiveSlot(slot) && !src.loop && !src.isPlaying)
					_pool.ReleaseSlot(slot);
			}
		}

		private static void Apply(AudioSource src, SoundParams p) {
			src.panStereo = p.Pan;
			src.pitch = p.Pitch;
			src.volume = p.Volume;
		}
	}
}
