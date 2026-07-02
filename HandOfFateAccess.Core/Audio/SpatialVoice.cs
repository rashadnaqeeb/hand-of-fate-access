using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// One playing sound, spatialized: a mono source (a registered clip read at a
	/// fractional step, a caller-supplied buffer, or a live synth) run through the
	/// shared placement chain - the rear low-pass wet/dry blend, then equal-power
	/// gains with the interaural delay on the far ear (see <see cref="Spatializer"/>).
	/// The chain is pure Core, so what the player actually hears renders and asserts
	/// in the offline tests.
	///
	/// Placement is re-settable while the voice plays: <see cref="SetParams"/> (main
	/// thread) writes volatile targets, and <see cref="MixInto"/> (audio thread) ramps
	/// the live values toward them across each block, so a moving source tracks
	/// click-free. After the source ends (or a stop is requested) the voice drains its
	/// delay tail, then reports finished for the engine's sweep to recycle.
	///
	/// Thread protocol: the audio thread touches the voice only while the volatile
	/// state says playing or stopping; the main thread configures it only while idle,
	/// publishing with the state write, and recycles it only once the audio thread
	/// has marked it finished. Finished is set exclusively by the audio thread, so the
	/// two never race on the same fields.
	/// </summary>
	public sealed class SpatialVoice {
		private const int StateIdle = 0;
		private const int StatePlaying = 1;
		private const int StateStopping = 2;
		private const int StateFinished = 3;

		// >= the max interaural delay (~32 samples at 48 kHz) plus margin; power of two.
		private const int RingSize = 64;
		private const int RingMask = RingSize - 1;

		/// <summary>The largest block <see cref="MixInto"/> is called with; the mixer
		/// chunks its fills to stay under it. Live-source scratch is sized to this.</summary>
		public const int MaxBlockFrames = 2048;

		private readonly int _sampleRate;
		private volatile int _state;

		// The source, one of two (set on the main thread while idle, published by the
		// state write): a mono clip read at a fractional step, or a live mono synth.
		private float[] _clip;
		private int _clipRate;
		private bool _loop;
		private IPcmSource _live;
		private float[] _liveScratch;   // lazily sized once, on the main thread
		private double _pos;            // clip read head; audio thread only after publish

		// Placement targets, written by SetParams (main thread), read per block on the
		// audio thread. A torn read is one slightly stale block.
		private volatile float _tGainL, _tGainR, _tItd, _tCutoff, _tWet, _tPitch;

		// Live smoothed values - audio thread only.
		private float _cGainL, _cGainR, _cItd, _cCutoff, _cWet;
		private bool _primed;

		private readonly float[] _ring = new float[RingSize];
		private readonly BiquadLowPass _lp = new BiquadLowPass();
		private long _frame;        // frames rendered this play (ring index)
		private bool _sourceEnded;
		private int _tail;          // drain frames left once the source ended

		public SpatialVoice(int sampleRate) {
			_sampleRate = sampleRate > 0 ? sampleRate : 44100;
		}

		/// <summary>Free for the engine to hand out.</summary>
		public bool IsIdle => _state == StateIdle;

		/// <summary>Drained; safe for the engine's sweep to <see cref="Recycle"/>.</summary>
		public bool IsFinished => _state == StateFinished;

		/// <summary>Start a mono clip. Looping plays until <see cref="RequestStop"/>;
		/// a one-shot finishes itself. Only valid on an idle voice (the engine's pool
		/// guarantees it). An empty clip finishes immediately.</summary>
		public void PlayClip(float[] mono, int clipRate, bool loop, SoundParams p) {
			if (mono == null || mono.Length == 0) {
				_state = StateFinished;
				return;
			}
			_clip = mono;
			_clipRate = clipRate > 0 ? clipRate : _sampleRate;
			_loop = loop;
			_live = null;
			Prepare(p);
		}

		/// <summary>Start a live mono source, pulled on the audio thread until
		/// <see cref="RequestStop"/>. A live source generates at the mixer rate, so
		/// the params' Pitch does not apply here. Only valid on an idle voice.</summary>
		public void PlayLive(IPcmSource source, SoundParams p) {
			_clip = null;
			_loop = false;
			_live = source;
			if (_liveScratch == null) _liveScratch = new float[MaxBlockFrames];
			Prepare(p);
		}

		private void Prepare(SoundParams p) {
			_pos = 0.0;
			_frame = 0;
			_sourceEnded = false;
			_tail = 0;
			Array.Clear(_ring, 0, RingSize);   // a fresh voice must not replay the last one's delay tail
			_lp.Reset();
			SetParams(p);
			_primed = false;
			_state = StatePlaying;   // volatile write: publishes everything above to the audio thread
		}

		/// <summary>Re-place a playing voice (pan, depth, pitch, volume); the audio
		/// thread ramps toward the new targets. Ignored once stopped.</summary>
		public void SetParams(SoundParams p) {
			SpatialCue cue = Spatializer.Cue(p, _sampleRate);
			_tGainL = cue.GainLeft;
			_tGainR = cue.GainRight;
			_tItd = cue.ItdSamples;
			_tCutoff = cue.CutoffHz;
			_tWet = cue.WetMix;
			_tPitch = p.Clamped().Pitch;
		}

		/// <summary>Begin the click-free stop: gains ramp to silence over the next
		/// block, the delay tail drains, then the voice reports finished.</summary>
		public void RequestStop() {
			if (_state == StatePlaying) _state = StateStopping;
		}

		/// <summary>Return a finished voice to idle (the engine's sweep, main thread).
		/// Drops the source references so a stopped clip or synth is collectable.</summary>
		public void Recycle() {
			if (_state != StateFinished) return;
			_clip = null;
			_live = null;
			_state = StateIdle;
		}

		/// <summary>Hard-drop a faulting voice (the mixer's fault path, audio thread):
		/// no fade, no drain - its source threw, so it is not pulled again. The sweep
		/// recycles the slot like any finished voice.</summary>
		public void Kill() {
			_state = StateFinished;
		}

		/// <summary>
		/// Add this voice's next <paramref name="frames"/> frames into the interleaved
		/// <paramref name="stereo"/> mix (which the mixer has already cleared). Audio
		/// thread only; never allocates, locks, or throws past the source's own Fill.
		/// </summary>
		public void MixInto(float[] stereo, int frames) {
			int state = _state;
			if (state != StatePlaying && state != StateStopping) return;

			_lp.SelfHeal();

			float tGainL = _tGainL, tGainR = _tGainR;
			float tItd = _tItd, tCutoff = _tCutoff, tWet = _tWet;
			if (state == StateStopping) {
				tGainL = 0f;
				tGainR = 0f;
			}

			// Prime on the first block; also self-heal if the ramp state ever went
			// non-finite (a NaN mixed into a running average never leaves on its own,
			// and a permanently NaN voice would poison the whole shared mix). Targets
			// are safe to snap to: SoundParams.Clamped neutralizes NaN fields.
			if (!_primed
					|| float.IsNaN(_cGainL) || float.IsNaN(_cGainR)
					|| float.IsNaN(_cItd) || float.IsNaN(_cCutoff) || float.IsNaN(_cWet)) {
				_cGainL = tGainL;
				_cGainR = tGainR;
				_cItd = tItd;
				_cCutoff = tCutoff;
				_cWet = tWet;
				_lp.SetCutoff(_sampleRate, _cCutoff);
				_primed = true;
			}

			// The cutoff converges once per block (retuning per sample is too costly;
			// the filter state is preserved across retunes, so the sweep is click-free).
			if (Math.Abs(tCutoff - _cCutoff) > 1f) {
				_cCutoff += (tCutoff - _cCutoff) * 0.5f;
				_lp.SetCutoff(_sampleRate, _cCutoff);
			}

			// One pull per block for a live source; the read loop then indexes it. A
			// stopping voice does not pull (its gains are ramping to silence anyway),
			// so once Stop is called the source is never touched again and its owner
			// can hand it to a fresh voice without two voices advancing its state.
			float[] liveBlock = null;
			if (_live != null && !_sourceEnded && state == StatePlaying) {
				liveBlock = _liveScratch;
				int pull = frames < MaxBlockFrames ? frames : MaxBlockFrames;
				_live.Fill(liveBlock, 1, pull);
			}
			double step = _tPitch * (_clipRate / (double)_sampleRate);

			// Gains, ITD, and wet mix ramp linearly to target across the block.
			float dGainL = (tGainL - _cGainL) / frames;
			float dGainR = (tGainR - _cGainR) / frames;
			float dItd = (tItd - _cItd) / frames;
			float dWet = (tWet - _cWet) / frames;

			int clipLen = _clip != null ? _clip.Length : 0;
			for (int f = 0; f < frames; f++) {
				float dry;
				if (_sourceEnded) {
					if (_tail <= 0) {
						_state = StateFinished;
						return;   // adds nothing further; the rest of the block is the mix's own silence
					}
					_tail--;
					dry = 0f;
				} else if (_live != null) {
					// liveBlock is null while stopping (the source is no longer pulled):
					// the fade block rides the ring tail down to silence.
					dry = liveBlock != null && f < MaxBlockFrames ? liveBlock[f] : 0f;
				} else {
					// Fractional clip read, linear-interpolated; wraps for a loop, ends
					// (into the tail drain) for a one-shot.
					if (_pos >= clipLen) {
						if (_loop) {
							_pos -= clipLen * Math.Floor(_pos / clipLen);
						} else {
							_sourceEnded = true;
							_tail = RingSize;
						}
					}
					if (_sourceEnded) {
						dry = 0f;
					} else {
						int i0 = (int)_pos;
						float a = _clip[i0];
						float b = i0 + 1 < clipLen ? _clip[i0 + 1] : (_loop ? _clip[0] : a);
						dry = a + (b - a) * (float)(_pos - i0);
						_pos += step;
					}
				}

				// Flush a NaN before it can scream; clamp so a hot source degrades, not clips.
				if (float.IsNaN(dry)) dry = 0f;
				else if (dry > 1f) dry = 1f;
				else if (dry < -1f) dry = -1f;

				_cGainL += dGainL;
				_cGainR += dGainR;
				_cItd += dItd;
				_cWet += dWet;

				// Rear muffle: blend the dry sample with its low-passed copy. The filter
				// always runs (state must stay warm for a cutoff sweep); ahead/beside the
				// wet mix is ~0, so the output is the dry sample bit-exact.
				float wet = _lp.Transform(dry);
				float m = dry + _cWet * (wet - dry);
				_ring[(int)(_frame & RingMask)] = m;

				// The far ear reads the ring a fractional delay back; the near ear is live.
				float itdMag = _cItd < 0f ? -_cItd : _cItd;
				int itdInt = (int)itdMag;
				if (itdInt > RingSize - 2) itdInt = RingSize - 2;
				float frac = itdMag - itdInt;
				long d0 = _frame - itdInt;
				long d1 = d0 - 1;
				float s0 = d0 >= 0 ? _ring[(int)(d0 & RingMask)] : 0f;
				float s1 = d1 >= 0 ? _ring[(int)(d1 & RingMask)] : 0f;
				float far = s0 + (s1 - s0) * frac;

				bool delayLeft = _cItd >= 0f;   // source right/east: the left ear lags
				stereo[f * 2] += (delayLeft ? far : m) * _cGainL;
				stereo[f * 2 + 1] += (delayLeft ? m : far) * _cGainR;
				_frame++;
			}

			// A stop drains: after the fade-out block, cut the source and run the tail.
			if (state == StateStopping && !_sourceEnded) {
				_sourceEnded = true;
				_tail = RingSize;
			}
		}
	}
}
