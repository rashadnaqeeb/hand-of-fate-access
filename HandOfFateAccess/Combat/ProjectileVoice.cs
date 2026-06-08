using System;
using HandOfFateAccess.Audio;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// One playing projectile voice. Its audio is synthesized on the audio thread by Core's
	/// <see cref="ProjectileSynth"/> (rhythm envelope times filtered noise, so pitch and
	/// tempo are independent); pan and volume are applied here as the mono output is spread
	/// to stereo. A silent looping clip keeps the AudioSource "playing" so the filter
	/// callback fires; the callback overwrites that silence with the generated audio.
	///
	/// Parameters are written from the main thread and read on the audio thread without a
	/// lock: each is a single field, so the worst case is one audio block at a slightly stale
	/// value. The callback never allocates and clamps its output, so a bad parameter degrades
	/// to quiet rather than a screech.
	/// </summary>
	internal sealed class ProjectileVoice : MonoBehaviour {
		private AudioSource _source;
		private ProjectileSynth _synth;
		private float[] _scratch;

		private volatile bool _playing;
		private float _pan;
		private float _volume;

		/// <summary>One-time setup: build the synth and an AudioSource fed a silent loop.
		/// Call on the main thread once the engine is live.</summary>
		public void Init(int seed, AudioClip silentLoop) {
			int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
			_synth = new ProjectileSynth(rate, (uint)seed);
			_scratch = new float[16384];  // headroom over any DSP block; never resized on the audio thread

			_source = gameObject.AddComponent<AudioSource>();
			_source.clip = silentLoop;
			_source.loop = true;
			_source.playOnAwake = false;
			_source.spatialBlend = 0f;   // 2D; pan is applied in the callback
			_source.volume = 1f;
			_source.panStereo = 0f;
		}

		public bool IsPlaying => _playing;

		public void Play(float pitch, float pan, float volume) {
			SetParams(pitch, pan, volume);
			_playing = true;
			_source.Play();
		}

		/// <summary>Re-aim a playing voice for this frame.</summary>
		public void SetParams(float pitch, float pan, float volume) {
			_synth.Pitch = pitch;
			_pan = pan < -1f ? -1f : (pan > 1f ? 1f : pan);
			_volume = volume < 0f ? 0f : (volume > 1f ? 1f : volume);
		}

		public void Stop() {
			_playing = false;
			if (_source != null) _source.Stop();
		}

		private void OnAudioFilterRead(float[] data, int channels) {
			if (!_playing || _synth == null) {
				Array.Clear(data, 0, data.Length);
				return;
			}
			int frames = data.Length / channels;
			if (frames > _scratch.Length) {        // unreachable in practice; never allocate here
				Array.Clear(data, 0, data.Length);
				return;
			}

			_synth.Process(_scratch, 0, frames);

			// Equal-power pan, so a centred voice is not louder than a hard-panned one.
			float vol = _volume;
			float angle = (_pan + 1f) * 0.25f * (float)Math.PI;   // 0..pi/2 across left..right
			float lGain = vol * (float)Math.Cos(angle);
			float rGain = vol * (float)Math.Sin(angle);

			int j = 0;
			for (int i = 0; i < frames; i++) {
				float s = _scratch[i];
				if (float.IsNaN(s)) s = 0f;         // flush NaN before it can scream
				else if (s > 1f) s = 1f;
				else if (s < -1f) s = -1f;
				if (channels >= 2) {
					data[j] = s * lGain;
					data[j + 1] = s * rGain;
					for (int c = 2; c < channels; c++) data[j + c] = 0f;
				} else {
					data[j] = s * vol;
				}
				j += channels;
			}
		}
	}
}
