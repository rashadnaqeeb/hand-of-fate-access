using System;
using UnityEngine;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Plays one mono buffer (a gambit identity tone or a rendered word) with live equal-power
	/// panning, mirroring the projectile voices: a silent looping clip keeps the AudioSource
	/// "playing" so the filter callback fires, and the callback overwrites that silence with the
	/// buffer, panned. Equal-power, not AudioSource.panStereo, so the gambit places sounds the
	/// same way as the validated prototype and the rest of the mod's spatial audio.
	///
	/// The mixing math lives in Core's <see cref="GambitVoiceDsp"/> (unit-tested); this is the
	/// thin engine shell. Parameters are written from the main thread and read on the audio
	/// thread without a lock: pan is a single field (a torn read is one stale block), and the
	/// buffer/step/loop are set before <see cref="_playing"/> flips true, which gates the read.
	/// </summary>
	internal sealed class GambitVoice : MonoBehaviour {
		private AudioSource _source;
		private int _outRate;

		private float[] _buffer;
		private double _pos;          // read position, owned by the audio thread only
		private double _step;
		private bool _loop;
		private float _pan;
		private float _volume;
		private volatile bool _playing;
		private volatile bool _resetPos;   // main thread asks the audio thread to rewind _pos

		public void Init(AudioClip silentLoop) {
			_outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
			_source = gameObject.AddComponent<AudioSource>();
			_source.clip = silentLoop;
			_source.loop = true;
			_source.playOnAwake = false;
			_source.spatialBlend = 0f;   // 2D; panning is applied in the callback
			_source.volume = 1f;
			_source.panStereo = 0f;
			_source.Play();
		}

		public bool IsPlaying => _playing;

		/// <summary>Starts playing <paramref name="buffer"/> (mono, at <paramref name="sourceRate"/>).
		/// Looping voices follow the shuffle; one-shots stop themselves at the end.</summary>
		public void Play(float[] buffer, int sourceRate, bool loop, float pan, float volume) {
			_playing = false;   // park the callback while we set up
			_buffer = buffer;
			_step = (sourceRate > 0 ? sourceRate : _outRate) / (double)_outRate;
			_loop = loop;
			_pan = pan;
			_volume = volume;
			// _pos is owned by the audio thread; ask it to rewind rather than writing the
			// double from here (a torn 64-bit write/read on x86 would corrupt the read head).
			// Ordered before _playing so the audio thread sees the request when it sees play.
			_resetPos = true;
			_playing = true;
		}

		/// <summary>Re-aims a playing voice this frame (the shuffle pans the tones live).</summary>
		public void SetPan(float pan) {
			_pan = pan;
		}

		public void Stop() {
			_playing = false;
		}

		private void OnAudioFilterRead(float[] data, int channels) {
			if (!_playing || _buffer == null) {
				Array.Clear(data, 0, data.Length);
				return;
			}
			if (_resetPos) { _pos = 0.0; _resetPos = false; }
			_pos = GambitVoiceDsp.Fill(_buffer, _pos, _step, _loop, _pan, _volume, data, channels, out bool finished);
			if (finished) _playing = false;
		}
	}
}
