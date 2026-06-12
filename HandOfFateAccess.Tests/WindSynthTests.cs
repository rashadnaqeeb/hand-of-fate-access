using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class WindSynthTests {
		private const int SampleRate = 44100;

		[Fact]
		public void Loop_HasTheAuthoredLength() {
			float[] loop = WindSynth.Render(SampleRate, 1u);
			Assert.Equal((int)(WindSynth.LoopSeconds * SampleRate + 0.5f), loop.Length);
		}

		[Fact]
		public void Loop_PeaksUnderUnity() {
			float[] loop = WindSynth.Render(SampleRate, 1u);
			float peak = 0f;
			for (int i = 0; i < loop.Length; i++) peak = Math.Max(peak, Math.Abs(loop[i]));
			Assert.InRange(peak, 0.1f, 1f);
		}

		[Fact]
		public void Loop_WrapsWithoutClick() {
			// The crossfade makes the wrap an ordinary adjacent-sample step of the raw
			// render, so the seam can be no larger than the biggest step in the body.
			float[] loop = WindSynth.Render(SampleRate, 1u);
			float maxStep = 0f;
			for (int i = 1; i < loop.Length; i++)
				maxStep = Math.Max(maxStep, Math.Abs(loop[i] - loop[i - 1]));
			float wrapStep = Math.Abs(loop[0] - loop[loop.Length - 1]);
			Assert.True(wrapStep <= maxStep,
				"wrap step " + wrapStep + " exceeds the body's largest step " + maxStep);
		}

		[Fact]
		public void SameSeed_ReproducesTheSameTake() {
			Assert.Equal(WindSynth.Render(SampleRate, 3u), WindSynth.Render(SampleRate, 3u));
		}

		[Fact]
		public void SideSeeds_AreDecorrelatedPairwise() {
			// Walls sound together routinely: left and right across a corridor or a column
			// pinch (at identical pitch, so the seeds are all that separates them), ahead
			// and behind in a narrow room, both in a corner. Every pair of the four side
			// seeds must be near-zero correlated, or the simultaneous winds fuse into one
			// phantom between their positions instead of imaging separately.
			var takes = new float[4][];
			for (uint s = 1; s <= 4; s++) takes[s - 1] = WindSynth.Render(SampleRate, s);
			for (int a = 0; a < 4; a++)
				for (int b = a + 1; b < 4; b++)
					Assert.InRange(Math.Abs(Correlation(takes[a], takes[b])), 0.0, 0.1);
		}

		private static double Correlation(float[] a, float[] b) {
			double sumA = 0, sumB = 0;
			for (int i = 0; i < a.Length; i++) { sumA += a[i]; sumB += b[i]; }
			double meanA = sumA / a.Length, meanB = sumB / b.Length;
			double num = 0, denA = 0, denB = 0;
			for (int i = 0; i < a.Length; i++) {
				double da = a[i] - meanA, db = b[i] - meanB;
				num += da * db;
				denA += da * da;
				denB += db * db;
			}
			return num / Math.Sqrt(denA * denB);
		}

		[Fact]
		public void ZeroSeed_StillRendersWind() {
			// The xorshift guard: seed 0 would otherwise freeze the generator at silence.
			float[] loop = WindSynth.Render(SampleRate, 0u);
			float peak = 0f;
			for (int i = 0; i < loop.Length; i++) peak = Math.Max(peak, Math.Abs(loop[i]));
			Assert.True(peak > 0.1f);
		}

		[Fact]
		public void DegenerateSampleRate_FallsBackInsteadOfThrowing() {
			float[] loop = WindSynth.Render(0, 1u);
			Assert.Equal((int)(WindSynth.LoopSeconds * 44100 + 0.5f), loop.Length);
		}
	}
}
