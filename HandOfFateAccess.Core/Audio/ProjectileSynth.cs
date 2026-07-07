using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Synthesizes the projectile tumble from scratch: a fixed amplitude envelope (a
	/// strong pulse and a weaker one per period) over a resonant low-pass on a noise
	/// source. The tumble's rhythm and brightness are its identity and never modulate
	/// with position - placement (pan, delay, pitch, volume) is applied by the
	/// spatial voice chain around it. The one identity variation is
	/// <see cref="Reflected"/>: the player's own bounced-back shot flutters faster, an
	/// orthogonal tag that scales the rhythm only.
	///
	/// Pure and engine-free, so it is unit-tested off-engine; the plugin owns one per
	/// voice and the chain pulls <see cref="Process"/> on the audio thread. Emits raw
	/// mono. <see cref="Reflected"/> is written from the main thread and read on the
	/// audio thread without a lock, a torn read being one harmless block.
	/// </summary>
	public sealed class ProjectileSynth {
		// Tumble rhythm (the tempo): a strong pulse at the period start and a weaker one
		// halfway through, each a fast attack into an exponential decay.
		public const float Period = 0.2f;        // seconds per tumble (5 Hz), the loop speed
		private const float Attack = 0.004f;
		private const float Decay = 0.05f;
		private const float WeakOffset = 0.1f;    // when the weak pulse starts within the period
		private const float WeakAmp = 0.55f;      // strong-then-weak, the tumble feel

		// A reflected projectile (the player's own shot, bounced back at the enemy) tumbles
		// faster: an orthogonal tag to pan/pitch/volume, so a reflected shot is instantly told
		// apart from an incoming threat while its bearing and distance still track normally. It
		// scales the rhythm only, never the cutoff, so the spatial cues are untouched.
		public const float ReflectedTempoScale = 1.8f;

		// Noise colour: a resonant low-pass over the noise source, the tumble's fixed
		// brightness.
		private const float BaseCutoff = 3000f;
		private const float Damp = 0.6f;          // state-variable damping; lower = more resonant
		private const float OutGain = 0.6f;

		private readonly int _sampleRate;
		private uint _rng;
		private float _low, _band;
		private float _tempo = 1f;   // tumble-rate multiplier; >1 flutters faster (reflected)

		// The tumble envelope is fixed and pitch-independent, so it is tabulated once at
		// construction and looked up per sample. This keeps the four Math.Exp calls its shape
		// needs off the audio thread, where with 32 voices they would be a real cost.
		private readonly float[] _env;
		private float _phasePos;   // fractional sample index into _env, advancing by _tempo per output sample

		/// <param name="seed">Per-voice noise seed; give concurrent voices different seeds so
		/// their noise does not correlate into a comb.</param>
		public ProjectileSynth(int sampleRate, uint seed) {
			_sampleRate = sampleRate > 0 ? sampleRate : 44100;
			_rng = seed == 0u ? 0x9E3779B9u : seed;

			int len = (int)(Period * _sampleRate + 0.5f);   // one tumble period, in samples
			_env = new float[len];
			for (int i = 0; i < len; i++)
				_env[i] = Env(i / (double)_sampleRate);
		}

		/// <summary>True for a reflected (outgoing) projectile: the tumble flutters faster so the
		/// player's own bounced-back shot is unmistakable against incoming threats, while its
		/// placement still tracks its bearing and distance. Set from the main thread, read
		/// on the audio thread.</summary>
		public bool Reflected {
			get { return _tempo > 1f; }
			set { _tempo = value ? ReflectedTempoScale : 1f; }
		}

		/// <summary>
		/// Render <paramref name="seconds"/> of the tumble offline into a buffer, for a
		/// registered demo clip (the sound glossary). Live projectiles synthesize in real
		/// time per voice instead; this exists so the sound can also be heard outside a
		/// fight, exactly as the authored tumble sounds.
		/// </summary>
		public static float[] Render(int sampleRate, float seconds, bool reflected, uint seed) {
			if (sampleRate <= 0) sampleRate = 44100;
			var synth = new ProjectileSynth(sampleRate, seed);
			synth.Reflected = reflected;
			var samples = new float[(int)(seconds * sampleRate)];
			synth.Process(samples, 0, samples.Length);
			return samples;
		}

		/// <summary>Fill <paramref name="count"/> mono samples into <paramref name="output"/>
		/// from <paramref name="offset"/>, advancing the rhythm and noise state.</summary>
		public void Process(float[] output, int offset, int count) {
			// Self-heal the filter if it ever reaches a non-finite state: otherwise a single
			// NaN would silence the voice permanently (NaN propagates through the integrator),
			// an invisible failure. Cheap, once per block.
			if (float.IsNaN(_low) || float.IsNaN(_band) || float.IsInfinity(_low) || float.IsInfinity(_band)) {
				_low = 0f;
				_band = 0f;
			}

			float fc = BaseCutoff;
			float maxFc = _sampleRate * 0.24f;     // keep the SVF stable below ~Nyquist/2
			if (fc > maxFc) fc = maxFc;
			float f = 2f * (float)Math.Sin(Math.PI * fc / _sampleRate);
			int len = _env.Length;

			float tempo = _tempo;
			if (!(tempo > 0f)) tempo = 1f;

			for (int n = 0; n < count; n++) {
				float input = Noise();
				_low += f * _band;
				float high = input - _low - Damp * _band;
				_band += f * high;
				output[offset + n] = _low * _env[(int)_phasePos] * OutGain;
				_phasePos += tempo;
				if (_phasePos >= len) _phasePos -= len;
			}
		}

		// xorshift32, mapped to [-1, 1).
		private float Noise() {
			_rng ^= _rng << 13;
			_rng ^= _rng >> 17;
			_rng ^= _rng << 5;
			return unchecked((int)_rng) * 4.656613e-10f;
		}

		// The periodic two-pulse envelope. Each pulse is summed with its previous-cycle tail
		// so the envelope is continuous across the period wrap (no click).
		private static float Env(double phase) {
			return Pulse(phase) + Pulse(phase + Period)
				+ WeakAmp * (Pulse(phase - WeakOffset) + Pulse(phase - WeakOffset + Period));
		}

		private static float Pulse(double t) {
			if (t < 0.0) return 0f;
			if (t < Attack) return (float)(t / Attack);
			return (float)Math.Exp(-(t - Attack) / Decay);
		}
	}
}
