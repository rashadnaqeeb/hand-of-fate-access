using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Renders the enemy-locator ping: the short, bright digital beep that answers the
	/// locator key from the nearest living enemy. Modeled on a reference beep sample
	/// (analyzed offline):
	/// a square-like stack of odd harmonics on a 414 Hz fundamental, a few milliseconds of
	/// attack, a fast exponential decay, and a faint ring that fades by the clip's end.
	/// Deliberately the only clean tonal blip in the combat mix: the wall wind is smooth
	/// noise, the projectile tumble is pulsed noise, the zone loops are a low 160 Hz buzz,
	/// and the telegraph cues are sampled impacts, so a ping can only mean an enemy.
	///
	/// Synthesized rather than shipped as the sample so the live pitch shift (the bearing
	/// grammar's north/south axis) starts from a clean authored tone, and rendered once at
	/// startup. Pure and engine-free; the plugin registers it as a one-shot clip.
	/// </summary>
	public static class EnemyPingSynth {
		public const string Key = "enemy_ping";

		public const float DurationSeconds = 0.25f;

		// The reference beep's measured shape. Odd harmonics only (square-wave family);
		// the rolloff exponent sits between a pure square (1.0) and the slightly brighter
		// stack the sample showed. Tunable by ear.
		private const float Fundamental = 414f;
		private const int TopHarmonic = 17;
		private const float HarmonicRolloff = 0.75f;

		// Envelope: linear attack, then a fast main decay plus a much quieter, slower ring
		// (both exponential), and a short fade forced to zero at the clip end so the
		// one-shot can never click off.
		private const float AttackSeconds = 0.006f;
		private const float DecaySeconds = 0.021f;
		private const float RingLevel = 0.09f;
		private const float RingDecaySeconds = 0.10f;
		private const float FadeOutSeconds = 0.03f;

		// Peak after normalization: loud enough to cut through a fight, headroom under
		// the telegraph cue samples.
		private const float PeakGain = 0.8f;

		public static float[] Render(int sampleRate) {
			if (sampleRate <= 0) sampleRate = 44100;
			int count = (int)(sampleRate * DurationSeconds);
			var samples = new float[count];

			float fadeSamples = FadeOutSeconds * sampleRate;
			for (int n = 0; n < count; n++) {
				float t = (float)n / sampleRate;

				float attack = t < AttackSeconds ? t / AttackSeconds : 1f;
				float decayTime = t < AttackSeconds ? 0f : t - AttackSeconds;
				// Unscaled sum of the two decay stages; overall level is set by the
				// normalization pass below, so no divisor is needed here.
				double env = attack * (Math.Exp(-decayTime / DecaySeconds)
					+ RingLevel * Math.Exp(-decayTime / RingDecaySeconds));
				float remaining = (count - 1 - n) / fadeSamples;
				if (remaining < 1f) env *= remaining;

				double tone = 0.0;
				for (int k = 1; k <= TopHarmonic; k += 2)
					tone += Math.Sin(2.0 * Math.PI * Fundamental * k * t) / Math.Pow(k, HarmonicRolloff);
				samples[n] = (float)(env * tone);
			}

			// Normalize numerically: the harmonic stack's true peak depends on phase
			// alignment, so scaling by the analytic amplitude sum would undershoot.
			float peak = 0f;
			for (int n = 0; n < count; n++) peak = Math.Max(peak, Math.Abs(samples[n]));
			float gain = PeakGain / peak;
			for (int n = 0; n < count; n++) samples[n] *= gain;
			return samples;
		}
	}
}
