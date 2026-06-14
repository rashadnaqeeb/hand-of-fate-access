using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// One projectile voice: a live PCM source the audio backend pulls. Its mono tumble is
	/// synthesized on the audio thread by Core's <see cref="ProjectileSynth"/> (rhythm envelope
	/// times filtered noise, so pitch and tempo are independent); <see cref="Fill"/> emits that
	/// raw mono, and the backend channel applies pan and volume (its mono pan is equal-power,
	/// the placement the rest of the mod's spatial audio uses). Each voice owns a backend synth
	/// voice registered once under its key; <see cref="Play"/> and <see cref="Stop"/> start and
	/// stop it as projectiles cycle through the pool.
	///
	/// Pan, volume, and pitch are pushed to the channel and synth from the main thread each
	/// frame. <see cref="Fill"/> runs on the audio thread; the only field it reads is the play
	/// gate, and it never allocates and clamps its output, so it degrades to quiet rather than a
	/// screech.
	/// </summary>
	internal sealed class ProjectileVoice : IPcmSource {
		private const int Channels = 1;   // mono; the channel pans the voice
		// Mono scratch headroom over any block the backend requests; never resized on the audio thread.
		private const int MaxBlockFrames = 2048;

		private readonly string _key;
		private readonly ProjectileSynth _synth;
		private readonly float[] _mono = new float[MaxBlockFrames];

		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private HandOfFateAccess.Audio.Voice _handle = HandOfFateAccess.Audio.Voice.None;
		private volatile bool _playing;
		private float _pan;
		private float _volume;

		/// <param name="key">Unique backend key for this voice's synth.</param>
		/// <param name="seed">Per-voice noise seed; concurrent voices get different seeds so their
		/// noise does not correlate into a comb.</param>
		/// <param name="sampleRate">Rate the synth generates at; matches the backend mixer rate.</param>
		public ProjectileVoice(string key, int seed, int sampleRate) {
			_key = key;
			_synth = new ProjectileSynth(sampleRate, (uint)seed);
			AudioEngine.RegisterSynth(key, this, Channels, sampleRate);
		}

		public bool IsPlaying => _playing;

		public void Play(float pitch, float pan, float volume, bool reflected) {
			SetParams(pitch, pan, volume, reflected);
			_playing = true;
			// Pitch lives in the synth (the cutoff), so the channel plays at unity pitch; pan and
			// volume drive the channel.
			_handle = AudioEngine.Play(_key, new SoundParams(_pan, 1f, _volume), true);
		}

		/// <summary>Re-aim a playing voice for this frame. <paramref name="reflected"/> flags the
		/// player's own bounced-back shot, which flutters faster so it is told apart from a threat.</summary>
		public void SetParams(float pitch, float pan, float volume, bool reflected) {
			_synth.Pitch = pitch;
			_synth.Reflected = reflected;
			_pan = pan < -1f ? -1f : (pan > 1f ? 1f : pan);
			_volume = volume < 0f ? 0f : (volume > 1f ? 1f : volume);
			if (_handle.IsValid)
				AudioEngine.Update(_handle, new SoundParams(_pan, 1f, _volume));
		}

		public void Stop() {
			_playing = false;
			if (_handle.IsValid) {
				AudioEngine.Stop(_handle);
				_handle = HandOfFateAccess.Audio.Voice.None;
			}
		}

		public void Fill(float[] buffer, int channels, int frames) {
			if (!_playing || frames > _mono.Length) {
				Array.Clear(buffer, 0, frames * channels);
				return;
			}

			_synth.Process(_mono, 0, frames);

			// Emit the raw tumble; the channel applies pan and volume. Written across however many
			// channels the backend asks for (mono in practice), so a non-mono request stays centred
			// for the channel pan to act on.
			int j = 0;
			for (int i = 0; i < frames; i++) {
				float s = _mono[i];
				if (float.IsNaN(s)) s = 0f;         // flush NaN before it can scream
				else if (s > 1f) s = 1f;
				else if (s < -1f) s = -1f;
				for (int c = 0; c < channels; c++) buffer[j + c] = s;
				j += channels;
			}
		}
	}
}
