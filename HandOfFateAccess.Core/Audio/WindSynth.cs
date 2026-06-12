using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Renders the wall wind: white noise through a slowly meandering band-pass, the one
	/// timbre all four wall tones share. Each side plays its own seed, so the takes are
	/// decorrelated copies of the same sound: simultaneous sides (a corner, a corridor)
	/// sum into one thicker wind yet still image at their own stereo positions, where
	/// identical copies would fuse into a single phantom somewhere between. The side a
	/// wind belongs to is carried at play time by pan (left/right) and by the bearing
	/// grammar's pitch axis (ahead bright and unshifted, behind darkest), not by a
	/// different recording, so a player learns one wall instrument, not three.
	///
	/// Seamless by construction where it can be: the gust and meander modulators complete
	/// whole cycles per loop, so they are identical on both sides of the wrap; the noise
	/// and filter state cannot wrap, so the render runs long and the tail is crossfaded
	/// into the head, leaving the seam an ordinary adjacent-sample step. Pure and
	/// engine-free, rendered once at startup and registered as looping clips.
	/// </summary>
	public static class WindSynth {
		/// <summary>Loop length. Long enough that the gusting does not read as a short
		/// repeating pattern; the slow modulators complete whole cycles in it.</summary>
		public const float LoopSeconds = 4f;

		// Band-pass center at pitch 1 (the unshifted ahead wind, the brightest the wind is
		// ever heard). Chosen so the south wind, the full pitch span down, still sits in a
		// clearly windy register rather than a rumble.
		private const float CenterHz = 4400f;

		// Octaves the band center wanders each way, the gusty brightness drift of wind.
		private const float MeanderOctaves = 0.4f;

		// State-variable filter damping: high, so the band is wide and breathy rather than
		// a whistling resonance.
		private const float Damp = 0.9f;

		// Depth of the slow loudness gusting: 0 is a steady hiss, 1 swells from silence.
		private const float GustDepth = 0.35f;

		// Tail length blended into the head to close the noise seam.
		private const float CrossfadeSeconds = 0.25f;

		// Normalized peak: headroom under the cue samples, in family with the other synths.
		private const float PeakGain = 0.7f;

		/// <summary>
		/// One wind loop from <paramref name="seed"/>. Different seeds give decorrelated
		/// takes of the same timbre; the same seed reproduces the same samples.
		/// </summary>
		public static float[] Render(int sampleRate, uint seed) {
			if (sampleRate <= 0) sampleRate = 44100;
			uint rng = seed == 0u ? 0x9E3779B9u : seed;

			int len = (int)(LoopSeconds * sampleRate + 0.5f);
			int fade = (int)(CrossfadeSeconds * sampleRate + 0.5f);
			var raw = new float[len + fade];

			// Modulator phases are the seed's other fingerprint, so two takes differ in
			// their gusting as well as their noise.
			float meanderPhase1 = NextPhase(ref rng);
			float meanderPhase2 = NextPhase(ref rng);
			float gustPhase = NextPhase(ref rng);

			float maxF = 2f * (float)Math.Sin(Math.PI * 0.24);   // keep the SVF stable
			float low = 0f, band = 0f;
			for (int n = 0; n < raw.Length; n++) {
				// Modulator time wraps at the loop length, not the rendered length, so the
				// overrun tail sees exactly the modulator state the head saw: the crossfade
				// blends two signals that differ only in noise and filter state.
				double t = (double)(n % len) / len;

				// The band center's slow wander: two integer-cycle sines, so the wrap is
				// seamless and the drift never reads as one metronomic sweep.
				double meander = (Math.Sin(2.0 * Math.PI * (1.0 * t) + meanderPhase1)
					+ 0.6 * Math.Sin(2.0 * Math.PI * (3.0 * t) + meanderPhase2)) / 1.6;
				float fc = CenterHz * (float)Math.Pow(2.0, MeanderOctaves * meander);
				float f = 2f * (float)Math.Sin(Math.PI * fc / sampleRate);
				if (f > maxF) f = maxF;

				double gust = 1.0 - GustDepth * (0.5 + 0.5 * Math.Sin(2.0 * Math.PI * (2.0 * t) + gustPhase));

				// xorshift32, mapped to [-1, 1), into the state-variable filter's band output.
				rng ^= rng << 13;
				rng ^= rng >> 17;
				rng ^= rng << 5;
				float input = unchecked((int)rng) * 4.656613e-10f;
				low += f * band;
				float high = input - low - Damp * band;
				band += f * high;

				raw[n] = (float)(band * gust);
			}

			// Fold the overrun tail into the head: at the wrap the loop continues into the
			// pure tail (an ordinary adjacent step of the raw render), and by the fade's end
			// it has handed over to the head's own state.
			var loop = new float[len];
			for (int i = 0; i < fade; i++) {
				float w = i / (float)fade;
				loop[i] = raw[len + i] * (1f - w) + raw[i] * w;
			}
			for (int i = fade; i < len; i++) loop[i] = raw[i];

			float peak = 0f;
			for (int i = 0; i < len; i++) {
				float a = Math.Abs(loop[i]);
				if (a > peak) peak = a;
			}
			if (peak > 0f) {
				float gain = PeakGain / peak;
				for (int i = 0; i < len; i++) loop[i] *= gain;
			}
			return loop;
		}

		// A uniform phase in [0, 2π) off the running generator.
		private static float NextPhase(ref uint rng) {
			rng ^= rng << 13;
			rng ^= rng >> 17;
			rng ^= rng << 5;
			return (float)(rng * (2.0 * Math.PI / 4294967296.0));
		}
	}
}
