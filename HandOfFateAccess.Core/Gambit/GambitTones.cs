using System;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Synthesizes each card's fixed identity tone for the gambit. Every slot is a distinct,
	/// memorable instrument that travels with its card: rising in register per slot, its own
	/// harmonic recipe, and its own motion. Slot 0 is an organ, 1 a violin, 2 a guitar, 3 a bell.
	/// Each voice owns one motion type so no slot is a midpoint of its neighbors: the organ
	/// throbs in loudness, the violin warbles in pitch (deep vibrato, no tremolo), the guitar
	/// and bell pluck, so each pulse is a fresh attack. The violin also sits a tritone off the
	/// other slots' shared C chroma (F#4, not C4): octave-spaced same-chroma tones are the most
	/// confusable interval, and the middle slot has no articulation contrast to fall back on.
	/// The low voices carry on full harmonic bodies (a bare sine at 130 Hz was too thin to
	/// localize and track). The player learns "this sound is this card" in the Establish walk,
	/// then follows that sound through the shuffle.
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
		private const float FadeSeconds = 0.008f;          // click-free in/out for the one-shot
		private const float PluckAttackFraction = 0.015f;  // fraction of a pluck period spent ramping up
		private const float PluckDecay = 0.30f;            // exp decay constant, in pluck-period fractions

		private enum Artic { Sustain, Pluck }

		// Per-slot identity. Indices past the table reuse the brightest (bell) recipe, plucked, at a
		// capped octave and a still-rising pulse; chance gambits past four cards are rare.
		private static readonly float[] Frequencies = { 130.81f, 369.99f, 523.25f, 1046.50f };
		private static readonly float[] PulseRates = { 2.5f, 0f, 6.0f, 10.0f };
		private static readonly Artic[] Articulations = { Artic.Sustain, Artic.Sustain, Artic.Pluck, Artic.Pluck };
		private static readonly float[] TremoloDepths = { 0.5f, 0f, 0f, 0f };   // organ only; the violin moves in pitch, not loudness
		private static readonly float[] VibratoDepths = { 0f, 0.05f, 0f, 0f };  // the violin's identity: nearly a semitone of warble
		private static readonly float[][] HarmonicSets = {
			new[] { 1.0f, 0.6f, 0.8f, 0.4f, 0.5f, 0.25f, 0.3f, 0.15f },    // organ: full drawbar stack so a low note carries
			new[] { 1.0f, 0.5f, 0.33f, 0.25f, 0.2f, 0.16f, 0.14f, 0.12f }, // violin: bowed sawtooth, vibrato added in Render
			new[] { 1.0f, 0.8f, 0.6f, 0.35f, 0.18f, 0.1f },               // guitar: warm pluck, harmonics 1-3 dominant
			new[] { 1.0f, 0.7f, 0.5f, 0.45f, 0.35f, 0.3f, 0.22f, 0.18f }, // bell: bright strike
		};

		/// <summary>The short Establish/probe one-shot for <paramref name="slotIndex"/>.</summary>
		public static float[] Generate(int slotIndex, int sampleRate) {
			slotIndex = Normalize(slotIndex);
			sampleRate = sampleRate > 0 ? sampleRate : 44100;
			int n = (int)(DurationSeconds * sampleRate + 0.5f);
			int fade = (int)(FadeSeconds * sampleRate + 0.5f);
			if (fade < 1) fade = 1;

			return Render(n, sampleRate, slotIndex,
				SlotFrequency(slotIndex, sampleRate), SlotPulseRate(slotIndex), SlotVibratoRate(slotIndex),
				EstablishAmplitude, fade);
		}

		/// <summary>The seamless shuffle loop for <paramref name="slotIndex"/>. Frequency, pulse and
		/// vibrato rates are quantized to a whole number of cycles over the buffer so it loops
		/// click-free: the carrier returns to phase zero at the wrap, a plucked voice's last pulse
		/// ends exactly at the buffer end, and the vibrato completes whole cycles so it adds no net
		/// phase across the seam.</summary>
		public static float[] GenerateSustain(int slotIndex, int sampleRate) {
			slotIndex = Normalize(slotIndex);
			sampleRate = sampleRate > 0 ? sampleRate : 44100;
			int n = (int)(SustainSeconds * sampleRate + 0.5f);

			double length = n / (double)sampleRate;
			float freq = Quantize(SlotFrequency(slotIndex, sampleRate), length);
			float pulse = Quantize(SlotPulseRate(slotIndex), length);
			float vibrato = Quantize(SlotVibratoRate(slotIndex), length);
			return Render(n, sampleRate, slotIndex, freq, pulse, vibrato, ShuffleAmplitude, 0);
		}

		// Additive harmonics over a phase-accumulated fundamental (so vibrato is a true frequency
		// modulation that the harmonics track), shaped by the slot's articulation: a sustained
		// tremolo swell for the organ/violin, or a per-pulse pluck envelope (sharp attack, exp
		// decay) for the guitar/bell. Optional linear fades over the first and last
		// <paramref name="fade"/> samples (fade 0 leaves the ends untouched, for a looping buffer).
		private static float[] Render(int n, int sampleRate, int slotIndex,
				float freq, float pulseRate, float vibratoRate, float amplitude, int fade) {
			float[] harmonics = SlotHarmonics(slotIndex);
			Artic artic = Articulations[Math.Min(slotIndex, Articulations.Length - 1)];
			float tremoloDepth = TremoloDepths[Math.Min(slotIndex, TremoloDepths.Length - 1)];
			float vibratoDepth = VibratoDepths[Math.Min(slotIndex, VibratoDepths.Length - 1)];

			float harmNorm = 0f;
			foreach (float h in harmonics) harmNorm += Math.Abs(h);
			if (harmNorm <= 0f) harmNorm = 1f;

			// Drop any harmonic at or above Nyquist: at high slots the fundamental is capped near
			// Nyquist, so its overtones would fold back (alias) into audible junk that corrupts
			// the slot's identity. Normalization keeps the full sum, so dropping some only quiets.
			float nyquist = sampleRate * 0.5f;

			float[] buffer = new float[n];
			const double twoPi = 2.0 * Math.PI;
			double phase = 0.0;   // accumulated fundamental phase; starts at zero (a zero crossing)
			double pluckPeriod = (artic == Artic.Pluck && pulseRate > 0f) ? 1.0 / pulseRate : 0.0;
			for (int i = 0; i < n; i++) {
				double t = i / (double)sampleRate;

				double sample = 0.0;
				for (int k = 0; k < harmonics.Length; k++)
					if (harmonics[k] != 0f && (k + 1) * freq < nyquist)
						sample += harmonics[k] * Math.Sin((k + 1) * phase);
				sample /= harmNorm;

				// Advance the fundamental phase, vibrato included so the harmonics wobble with it.
				double f = vibratoDepth != 0f
					? freq * (1.0 + vibratoDepth * Math.Sin(twoPi * vibratoRate * t))
					: freq;
				phase += twoPi * f / sampleRate;

				float artGain;
				if (pluckPeriod > 0.0) {
					double ph = (t % pluckPeriod) / pluckPeriod;   // 0..1 within the current pluck
					artGain = ph < PluckAttackFraction
						? (float)(ph / PluckAttackFraction)
						: (float)Math.Exp(-(ph - PluckAttackFraction) / PluckDecay);
				} else if (pulseRate > 0f && tremoloDepth > 0f) {
					artGain = 1f - tremoloDepth * 0.5f * (1f + (float)Math.Sin(twoPi * pulseRate * t));
				} else {
					artGain = 1f;
				}

				float env = 1f;
				if (fade > 0) {
					if (i < fade) env = i / (float)fade;
					else if (i >= n - fade) env = (n - 1 - i) / (float)fade;
				}
				buffer[i] = (float)sample * artGain * env * amplitude;
			}
			return buffer;
		}

		// Round a rate to a whole number of cycles over a buffer of the given length, so it tiles
		// the loop seamlessly. A zero rate (no vibrato) stays zero.
		private static float Quantize(float rate, double length) =>
			(float)(Math.Round(rate * length) / length);

		private static int Normalize(int slotIndex) => slotIndex < 0 ? 0 : slotIndex;

		private static float[] SlotHarmonics(int slotIndex) =>
			HarmonicSets[Math.Min(slotIndex, HarmonicSets.Length - 1)];

		private static float SlotPulseRate(int slotIndex) =>
			slotIndex < PulseRates.Length
				? PulseRates[slotIndex]
				: PulseRates[PulseRates.Length - 1] + (slotIndex - PulseRates.Length + 1) * 4f;

		private static float SlotVibratoRate(int slotIndex) =>
			slotIndex < VibratoDepths.Length && VibratoDepths[slotIndex] != 0f ? 5.5f : 0f;

		private static float SlotFrequency(int slotIndex, int sampleRate) {
			float freq = slotIndex < Frequencies.Length
				? Frequencies[slotIndex]
				: 130.81f * (float)Math.Pow(2.0, slotIndex);
			float max = sampleRate * 0.45f;   // stay below Nyquist for high slots
			return freq > max ? max : freq;
		}
	}
}
