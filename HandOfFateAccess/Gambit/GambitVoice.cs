using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// One gambit voice: a live PCM source the audio backend pulls. It plays a mono buffer (an
	/// identity tone or a rendered word) with live equal-power panning, the placement the
	/// validated prototype and the rest of the mod's spatial audio use. The mixing math lives in
	/// Core's <see cref="GambitVoiceDsp"/> (unit-tested), which resamples the buffer and pans it
	/// into interleaved stereo; this is the thin engine shell. The voice registers a stereo
	/// backend synth (the DSP pans itself, so it plays through a neutral channel).
	///
	/// Parameters are written from the main thread and read on the audio thread without a lock:
	/// pan is a single field (a torn read is one stale block), and the buffer/step/loop are set
	/// before <see cref="_playing"/> flips true, which gates the read.
	/// </summary>
	internal sealed class GambitVoice : IPcmSource {
		private const int Channels = 2;   // stereo; the DSP pans, the channel stays neutral

		private readonly string _key;
		private readonly int _outRate;

		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private HandOfFateAccess.Audio.Voice _handle = HandOfFateAccess.Audio.Voice.None;

		private float[] _buffer;
		private double _pos;          // read position, owned by the audio thread only
		private double _step;
		private bool _loop;
		private float _pan;
		private float _volume;
		private volatile bool _playing;
		private volatile bool _resetPos;   // main thread asks the audio thread to rewind _pos

		/// <param name="key">Unique backend key for this voice's synth.</param>
		/// <param name="outRate">The backend mixer rate the DSP fills at; the source rate passed to
		/// <see cref="Play"/> is resampled to it.</param>
		public GambitVoice(string key, int outRate) {
			_key = key;
			_outRate = outRate > 0 ? outRate : 44100;
			AudioEngine.RegisterSynth(key, this, Channels, _outRate);
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
			// _pos is owned by the audio thread; ask it to rewind rather than writing the double
			// from here (a torn 64-bit write/read on x86 would corrupt the read head). Ordered
			// before _playing so the audio thread sees the request when it sees play.
			_resetPos = true;
			_playing = true;
			// Start the backend voice once and reuse it; subsequent Plays swap the buffer above.
			if (!_handle.IsValid)
				_handle = AudioEngine.Play(_key, SoundParams.Neutral, true);
		}

		/// <summary>Re-aims a playing voice this frame (the shuffle pans the tones live).</summary>
		public void SetPan(float pan) {
			_pan = pan;
		}

		public void Stop() {
			_playing = false;
			if (_handle.IsValid) {
				AudioEngine.Stop(_handle);
				_handle = HandOfFateAccess.Audio.Voice.None;
			}
		}

		public void Fill(float[] buffer, int channels, int frames) {
			if (!_playing || _buffer == null) {
				Array.Clear(buffer, 0, frames * channels);
				return;
			}
			if (_resetPos) { _pos = 0.0; _resetPos = false; }
			_pos = GambitVoiceDsp.Fill(_buffer, _pos, _step, _loop, _pan, _volume, buffer, channels, frames, out bool finished);
			if (finished) _playing = false;
		}
	}
}
