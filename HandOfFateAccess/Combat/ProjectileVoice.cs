using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// One projectile voice: a live mono PCM source the spatial chain pulls. The
	/// tumble is synthesized on the audio thread by Core's <see cref="ProjectileSynth"/>
	/// (rhythm envelope times filtered noise); the chain applies all placement (pan,
	/// interaural delay, rear muffle, volume) around the raw mono this emits.
	/// <see cref="Play"/> and <see cref="Stop"/> start and stop it as projectiles
	/// cycle through the pool; placement is pushed each frame with
	/// <see cref="SetParams"/>.
	///
	/// <see cref="Fill"/> runs on the audio thread; the only field it reads is the
	/// play gate, and it never allocates and clamps its output, so it degrades to
	/// quiet rather than a screech.
	/// </summary>
	internal sealed class ProjectileVoice : IPcmSource {
		private readonly ProjectileSynth _synth;

		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private HandOfFateAccess.Audio.Voice _handle = HandOfFateAccess.Audio.Voice.None;
		private volatile bool _playing;

		/// <param name="seed">Per-voice noise seed; concurrent voices get different seeds so their
		/// noise does not correlate into a comb.</param>
		/// <param name="sampleRate">Rate the synth generates at; matches the backend mixer rate.</param>
		public ProjectileVoice(int seed, int sampleRate) {
			_synth = new ProjectileSynth(sampleRate, (uint)seed);
		}

		public bool IsPlaying => _playing;

		/// <summary><paramref name="reflected"/> flags the player's own bounced-back shot,
		/// which flutters faster so it is told apart from a threat.</summary>
		public void Play(SoundParams parameters, bool reflected) {
			_synth.Reflected = reflected;
			_playing = true;
			_handle = AudioEngine.PlayLive(this, parameters);
		}

		/// <summary>Re-aim a playing voice for this frame.</summary>
		public void SetParams(SoundParams parameters, bool reflected) {
			_synth.Reflected = reflected;
			if (_handle.IsValid)
				AudioEngine.Update(_handle, parameters);
		}

		public void Stop() {
			_playing = false;
			// The handle is kept: the engine voice fades and drains for a couple of
			// blocks after Stop, and IsDrained (below) is how the pool knows this
			// source is safe to hand to a new engine voice.
			if (_handle.IsValid)
				AudioEngine.Stop(_handle);
		}

		/// <summary>Whether the last engine voice this source played on has fully
		/// drained. Until then the source must not be re-Played: two engine voices
		/// pulling one synth would chop its tumble and sound a ghost at the dead
		/// projectile's bearing.</summary>
		public bool IsDrained => !_handle.IsValid || !AudioEngine.IsPlaying(_handle);

		public void Fill(float[] buffer, int channels, int frames) {
			// The chain pulls mono; anything else would be a misuse, silenced rather
			// than screeched (this cannot log from the audio thread).
			if (!_playing || channels != 1) {
				Array.Clear(buffer, 0, frames * channels);
				return;
			}

			_synth.Process(buffer, 0, frames);
			for (int i = 0; i < frames; i++) {
				float s = buffer[i];
				if (float.IsNaN(s)) buffer[i] = 0f;   // flush NaN before it can scream
				else if (s > 1f) buffer[i] = 1f;
				else if (s < -1f) buffer[i] = -1f;
			}
		}
	}
}
