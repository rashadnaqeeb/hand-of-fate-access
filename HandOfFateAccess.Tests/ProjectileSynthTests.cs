using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ProjectileSynthTests {
		private const int SampleRate = 44100;

		private static float[] Render(float pitch, uint seed, int samples) {
			var synth = new ProjectileSynth(SampleRate, seed) { Pitch = pitch };
			var buf = new float[samples];
			synth.Process(buf, 0, buf.Length);
			return buf;
		}

		private static double Rms(float[] buf) {
			double sum = 0;
			foreach (float s in buf) sum += (double)s * s;
			return Math.Sqrt(sum / buf.Length);
		}

		// Mean absolute first difference: a brightness proxy that rises with high-frequency
		// content, so it rises with pitch (the cutoff).
		private static double Brightness(float[] buf) {
			double sum = 0;
			for (int i = 1; i < buf.Length; i++) sum += Math.Abs(buf[i] - buf[i - 1]);
			return sum / (buf.Length - 1);
		}

		[Fact]
		public void ProducesFiniteAudio() {
			foreach (float s in Render(0.7f, 1u, SampleRate))
				Assert.False(float.IsNaN(s) || float.IsInfinity(s));
		}

		[Fact]
		public void NotSilent() {
			Assert.True(Rms(Render(1f, 1u, SampleRate)) > 0.01);
		}

		[Fact]
		public void HigherPitch_IsBrighter() {
			double bright = Brightness(Render(1f, 7u, SampleRate));
			double dark = Brightness(Render(0.5f, 7u, SampleRate));
			Assert.True(bright > dark, $"expected brighter {bright} > darker {dark}");
		}

		[Fact]
		public void TempoIndependentOfPitch() {
			// Same noise seed, two pitches: the filter colour differs but the tumble envelope
			// is identical, so the smoothed amplitude envelopes line up tightly. If pitch were
			// leaking into the rhythm (a resampling artifact), this correlation would fall.
			float[] a = Render(1f, 42u, SampleRate);
			float[] b = Render(0.5f, 42u, SampleRate);
			double[] ea = Envelope(a), eb = Envelope(b);

			double ma = Mean(ea), mb = Mean(eb), cov = 0, va = 0, vb = 0;
			for (int i = 0; i < ea.Length; i++) {
				double da = ea[i] - ma, db = eb[i] - mb;
				cov += da * db; va += da * da; vb += db * db;
			}
			double corr = cov / Math.Sqrt(va * vb);
			Assert.True(corr > 0.85, $"envelope correlation {corr} too low; pitch is bleeding into tempo");
		}

		// Rectified, box-smoothed amplitude envelope (~5 ms window).
		private static double[] Envelope(float[] buf) {
			int w = SampleRate / 200;
			var env = new double[buf.Length];
			double run = 0;
			for (int i = 0; i < buf.Length; i++) {
				run += Math.Abs(buf[i]);
				if (i >= w) run -= Math.Abs(buf[i - w]);
				env[i] = run / Math.Min(i + 1, w);
			}
			return env;
		}

		private static double Mean(double[] x) {
			double s = 0;
			foreach (double v in x) s += v;
			return s / x.Length;
		}
	}
}
