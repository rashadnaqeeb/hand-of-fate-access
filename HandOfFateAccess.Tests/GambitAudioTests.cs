using System;
using HandOfFateAccess.Gambit;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class GambitLayoutTests {
		[Fact]
		public void SingleCard_IsCentred() {
			Assert.Equal(0f, GambitLayout.SlotPan(0, 1));
		}

		[Fact]
		public void Endpoints_AreHardLeftAndRight() {
			Assert.Equal(-1f, GambitLayout.SlotPan(0, 4));
			Assert.Equal(1f, GambitLayout.SlotPan(3, 4));
			Assert.Equal(-1f, GambitLayout.SlotPan(0, 2));
			Assert.Equal(1f, GambitLayout.SlotPan(1, 2));
		}

		[Fact]
		public void FourCards_MatchValidatedWideLayout() {
			// The prototype layout, inner cards pushed out to +-0.5 rather than +-0.33.
			Assert.Equal(-1f, GambitLayout.SlotPan(0, 4), 5);
			Assert.Equal(-0.5f, GambitLayout.SlotPan(1, 4), 5);
			Assert.Equal(0.5f, GambitLayout.SlotPan(2, 4), 5);
			Assert.Equal(1f, GambitLayout.SlotPan(3, 4), 5);
		}

		[Fact]
		public void InteriorSlots_Symmetric() {
			Assert.Equal(0f, GambitLayout.SlotPan(1, 4) + GambitLayout.SlotPan(2, 4), 5);
			Assert.Equal(0f, GambitLayout.SlotPan(1, 3), 5);   // centre card of three
		}

		[Fact]
		public void Pan_RisesLeftToRight() {
			for (int i = 1; i < 5; i++)
				Assert.True(GambitLayout.SlotPan(i, 5) > GambitLayout.SlotPan(i - 1, 5));
		}
	}

	public class GambitTonesTests {
		private const int SampleRate = 44100;

		private static double Brightness(float[] buf) {
			double sum = 0;
			for (int i = 1; i < buf.Length; i++) sum += Math.Abs(buf[i] - buf[i - 1]);
			return sum / (buf.Length - 1);
		}

		private static double Rms(float[] buf) {
			double sum = 0;
			foreach (float s in buf) sum += (double)s * s;
			return Math.Sqrt(sum / buf.Length);
		}

		[Fact]
		public void Length_MatchesDuration() {
			int expected = (int)(GambitTones.DurationSeconds * SampleRate + 0.5f);
			Assert.Equal(expected, GambitTones.Generate(0, SampleRate).Length);
		}

		[Fact]
		public void ProducesFiniteAudioWithinAmplitude() {
			for (int slot = 0; slot < 4; slot++)
				foreach (float s in GambitTones.Generate(slot, SampleRate)) {
					Assert.False(float.IsNaN(s) || float.IsInfinity(s));
					Assert.True(Math.Abs(s) <= 0.5f + 1e-4f, $"sample {s} exceeds Establish amplitude");
				}
		}

		[Fact]
		public void FadesFromAndToSilence() {
			foreach (int slot in new[] { 0, 3 }) {
				float[] buf = GambitTones.Generate(slot, SampleRate);
				Assert.Equal(0f, buf[0], 5);
				Assert.Equal(0f, buf[buf.Length - 1], 5);
			}
		}

		[Fact]
		public void NotSilent() {
			Assert.True(Rms(GambitTones.Generate(0, SampleRate)) > 0.01);
		}

		[Fact]
		public void HigherSlot_IsBrighter() {
			double high = Brightness(GambitTones.Generate(3, SampleRate));
			double low = Brightness(GambitTones.Generate(0, SampleRate));
			Assert.True(high > low, $"expected slot 3 brighter {high} > slot 0 {low}");
		}

		[Fact]
		public void DistinctSlots_DifferInTimbre() {
			float[] a = GambitTones.Generate(0, SampleRate);
			float[] b = GambitTones.Generate(1, SampleRate);
			bool differ = false;
			for (int i = 0; i < a.Length; i++)
				if (Math.Abs(a[i] - b[i]) > 1e-3f) { differ = true; break; }
			Assert.True(differ, "adjacent slot tones are identical");
		}

		[Fact]
		public void Sustain_LengthMatchesLoopDuration() {
			int expected = (int)(GambitTones.SustainSeconds * SampleRate + 0.5f);
			Assert.Equal(expected, GambitTones.GenerateSustain(0, SampleRate).Length);
		}

		[Fact]
		public void Sustain_IsFiniteBoundedAndAudible() {
			for (int slot = 0; slot < 4; slot++) {
				float[] buf = GambitTones.GenerateSustain(slot, SampleRate);
				foreach (float s in buf) {
					Assert.False(float.IsNaN(s) || float.IsInfinity(s));
					Assert.True(Math.Abs(s) <= 0.30f + 1e-4f, $"sample {s} exceeds shuffle amplitude");
				}
				Assert.True(Rms(buf) > 0.01, "sustain loop is silent");
			}
		}

		[Fact]
		public void Sustain_LoopsSeamlessly() {
			// Frequency and tremolo are quantized to whole cycles over the buffer, so it holds
			// an integer number of periods and the sample after the end equals the first sample
			// (buf[n] == buf[0]). Seamlessness therefore means the step across the seam
			// (buf[n-1] into buf[0]) is no larger than the waveform's normal internal steps, not
			// that it is tiny: a high, bright tone has large adjacent-sample steps at its zero
			// crossings, and the seam sits at one. A seam step exceeding the internal maximum
			// would be a real discontinuity, i.e. an audible loop click.
			foreach (int slot in new[] { 0, 1, 2, 3 }) {
				float[] buf = GambitTones.GenerateSustain(slot, SampleRate);
				Assert.Equal(0f, buf[0], 4);
				float maxStep = 0f;
				for (int i = 1; i < buf.Length; i++)
					maxStep = Math.Max(maxStep, Math.Abs(buf[i] - buf[i - 1]));
				float seamStep = Math.Abs(buf[0] - buf[buf.Length - 1]);
				Assert.True(seamStep <= maxStep + 1e-5f,
					$"slot {slot} seam step {seamStep} exceeds internal max {maxStep}");
			}
		}

		[Fact]
		public void DropsHarmonicsAboveNyquist() {
			// At a low sample rate, slot 3's upper harmonics exceed Nyquist and must be dropped,
			// so the tone reduces to exactly its sub-Nyquist harmonics. Rebuild that expected
			// tone here with the same synthesis math but only the surviving harmonics, and
			// require a match. If the guard were removed, the generated tone would include the
			// aliased harmonics and diverge, failing this test.
			const int rate = 6000;   // Nyquist 3000
			float[] actual = GambitTones.Generate(3, rate);

			// Slot 3 identity: 1046.5 Hz, tremolo 10 Hz, harmonics [1, .5, .33, .25, .20, .16].
			// Surviving harmonics k where (k+1)*1046.5 < 3000: only k=0 (1046.5) and k=1 (2093).
			const float freq = 1046.5f, tremolo = 10f, amplitude = 0.5f, tremoloDepth = 0.6f;
			float[] harm = { 1f, 0.5f, 0.33f, 0.25f, 0.20f, 0.16f };
			float harmNorm = 0f;
			foreach (float h in harm) harmNorm += Math.Abs(h);
			int n = (int)(0.32f * rate + 0.5f);
			int fade = (int)(0.008f * rate + 0.5f);
			float nyquist = rate * 0.5f;
			const double twoPi = 2.0 * Math.PI;

			Assert.Equal(n, actual.Length);
			bool droppedAny = false;
			for (int i = 0; i < n; i++) {
				double t = i / (double)rate;
				double phase = twoPi * freq * t;
				double sample = 0.0;
				for (int k = 0; k < harm.Length; k++) {
					if ((k + 1) * freq >= nyquist) { droppedAny = true; continue; }
					sample += harm[k] * Math.Sin((k + 1) * phase);
				}
				sample /= harmNorm;
				float trem = 1f - tremoloDepth * 0.5f * (1f + (float)Math.Sin(twoPi * tremolo * t));
				float env = 1f;
				if (i < fade) env = i / (float)fade;
				else if (i >= n - fade) env = (n - 1 - i) / (float)fade;
				Assert.Equal((float)sample * trem * env * amplitude, actual[i], 4);
			}
			Assert.True(droppedAny, "test rate did not actually force any harmonic to be dropped");
		}

		[Fact]
		public void HighSlot_StaysBelowNyquist() {
			// Slot 8's natural octave is far above Nyquist; it must be capped, not aliased into
			// a buffer that still has to be finite and bounded.
			foreach (float s in GambitTones.Generate(8, SampleRate)) {
				Assert.False(float.IsNaN(s) || float.IsInfinity(s));
				Assert.True(Math.Abs(s) <= 0.5f + 1e-4f);
			}
		}
	}
}
