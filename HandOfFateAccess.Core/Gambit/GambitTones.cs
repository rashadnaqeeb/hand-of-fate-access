using System;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Synthesizes each card's fixed identity tone for the gambit. Every slot gets a distinct,
	/// memorable timbre that travels with its card: one octave higher per slot, a richer
	/// harmonic recipe per slot, and a faster tremolo per slot. The player learns "this sound
	/// is this card" in the Establish walk, then follows that sound through the shuffle.
	///
	/// Two voicings share the timbre. The Establish tone (<see cref="Generate"/>) is a short
	/// one-shot with fades, played per slot as it is taught and again as a probe while picking.
	/// The shuffle voice (<see cref="GenerateSustain"/>) is a seamless loop, sustained and
	/// panned to follow the card while it moves. Pure and engine-free, so it is unit-tested
	/// off-engine; the plugin generates each slot's buffers once and registers them as clips.
	/// </summary>
	public static class GambitTones {
		/// <summary>Length of one Establish tone. Also the minimum a slot occupies in the walk.</summary>
		public const float DurationSeconds = 0.32f;
		/// <summary>Length of one shuffle-voice loop; it repeats for the whole shuffle.</summary>
		public const float SustainSeconds = 2.0f;

		private const float EstablishAmplitude = 0.5f;
		private const float ShuffleAmplitude = 0.30f;
		private const float FadeSeconds = 0.008f;   // click-free in/out for the one-shot
		private const float TremoloDepth = 0.6f;    // amplitude swings between 0.4 and 1.0

		// Per-slot identity. Indices past the table reuse the brightest harmonic recipe at a
		// capped octave and a still-rising tremolo; chance gambits past four cards are rare.
		private static readonly float[] Frequencies = { 130.81f, 261.63f, 523.25f, 1046.50f };
		private static readonly float[] TremoloRates = { 2.0f, 3.5f, 6.0f, 10.0f };
		private static readonly float[][] HarmonicSets = {
			new[] { 1.0f },
			new[] { 1.0f, 0f, 0.12f, 0f, 0.04f },
			new[] { 1.0f, 0f, 0.33f, 0f, 0.20f, 0f, 0.14f },
			new[] { 1.0f, 0.5f, 0.33f, 0.25f, 0.20f, 0.16f },
		};

		/// <summary>The short Establish/probe one-shot for <paramref name="slotIndex"/>.</summary>
		public static float[] Generate(int slotIndex, int sampleRate) {
			slotIndex = Normalize(slotIndex);
			sampleRate = sampleRate > 0 ? sampleRate : 44100;
			int n = (int)(DurationSeconds * sampleRate + 0.5f);
			int fade = (int)(FadeSeconds * sampleRate + 0.5f);
			if (fade < 1) fade = 1;

			float freq = SlotFrequency(slotIndex, sampleRate);
			float tremolo = SlotTremolo(slotIndex);
			return Render(n, sampleRate, freq, tremolo, SlotHarmonics(slotIndex), EstablishAmplitude, fade);
		}

		/// <summary>The seamless shuffle loop for <paramref name="slotIndex"/>. Frequency and
		/// tremolo are quantized to an integer number of cycles over the buffer so it loops
		/// click-free (both return to phase zero at the wrap, where the waveform is also zero).</summary>
		public static float[] GenerateSustain(int slotIndex, int sampleRate) {
			slotIndex = Normalize(slotIndex);
			sampleRate = sampleRate > 0 ? sampleRate : 44100;
			int n = (int)(SustainSeconds * sampleRate + 0.5f);

			double length = n / (double)sampleRate;
			float freq = (float)(Math.Round(SlotFrequency(slotIndex, sampleRate) * length) / length);
			float tremolo = (float)(Math.Round(SlotTremolo(slotIndex) * length) / length);
			return Render(n, sampleRate, freq, tremolo, SlotHarmonics(slotIndex), ShuffleAmplitude, 0);
		}

		// Additive harmonics times amplitude tremolo, optionally with linear fades over the
		// first and last <paramref name="fade"/> samples (fade 0 leaves the ends untouched,
		// for a looping buffer).
		private static float[] Render(int n, int sampleRate, float freq, float tremolo,
				float[] harmonics, float amplitude, int fade) {
			float harmNorm = 0f;
			foreach (float h in harmonics) harmNorm += Math.Abs(h);
			if (harmNorm <= 0f) harmNorm = 1f;

			// Drop any harmonic at or above Nyquist: at high slots the fundamental is capped near
			// Nyquist, so its overtones would fold back (alias) into audible junk that corrupts
			// the slot's identity. Normalization keeps the full sum, so dropping some only quiets.
			float nyquist = sampleRate * 0.5f;

			float[] buffer = new float[n];
			const double twoPi = 2.0 * Math.PI;
			for (int i = 0; i < n; i++) {
				double t = i / (double)sampleRate;
				double phase = twoPi * freq * t;

				double sample = 0.0;
				for (int k = 0; k < harmonics.Length; k++)
					if (harmonics[k] != 0f && (k + 1) * freq < nyquist)
						sample += harmonics[k] * Math.Sin((k + 1) * phase);
				sample /= harmNorm;

				float tremGain = tremolo > 0f
					? 1f - TremoloDepth * 0.5f * (1f + (float)Math.Sin(twoPi * tremolo * t))
					: 1f;
				float env = 1f;
				if (fade > 0) {
					if (i < fade) env = i / (float)fade;
					else if (i >= n - fade) env = (n - 1 - i) / (float)fade;
				}
				buffer[i] = (float)sample * tremGain * env * amplitude;
			}
			return buffer;
		}

		private static int Normalize(int slotIndex) => slotIndex < 0 ? 0 : slotIndex;

		private static float[] SlotHarmonics(int slotIndex) =>
			HarmonicSets[Math.Min(slotIndex, HarmonicSets.Length - 1)];

		private static float SlotTremolo(int slotIndex) =>
			slotIndex < TremoloRates.Length
				? TremoloRates[slotIndex]
				: TremoloRates[TremoloRates.Length - 1] + (slotIndex - TremoloRates.Length + 1) * 4f;

		private static float SlotFrequency(int slotIndex, int sampleRate) {
			float freq = slotIndex < Frequencies.Length
				? Frequencies[slotIndex]
				: 130.81f * (float)Math.Pow(2.0, slotIndex);
			float max = sampleRate * 0.45f;   // stay below Nyquist for high slots
			return freq > max ? max : freq;
		}
	}
}
