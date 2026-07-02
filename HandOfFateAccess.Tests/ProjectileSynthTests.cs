using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ProjectileSynthTests {
		private const int SampleRate = 44100;

		private static float[] Render(uint seed, int samples) {
			var synth = new ProjectileSynth(SampleRate, seed);
			var buf = new float[samples];
			synth.Process(buf, 0, buf.Length);
			return buf;
		}

		private static double Rms(float[] buf) {
			double sum = 0;
			foreach (float s in buf) sum += (double)s * s;
			return Math.Sqrt(sum / buf.Length);
		}

		[Fact]
		public void ProducesFiniteAudio() {
			foreach (float s in Render(1u, SampleRate))
				Assert.False(float.IsNaN(s) || float.IsInfinity(s));
		}

		[Fact]
		public void NotSilent() {
			Assert.True(Rms(Render(1u, SampleRate)) > 0.01);
		}

		[Fact]
		public void Reflected_TumblesFaster() {
			// Same seed, so the only difference is the tumble rate. A reflected voice
			// flutters faster, so its smoothed envelope crosses its own mean upward more often.
			var normal = new ProjectileSynth(SampleRate, 9u);
			var reflected = new ProjectileSynth(SampleRate, 9u) { Reflected = true };
			var nbuf = new float[SampleRate];
			var rbuf = new float[SampleRate];
			normal.Process(nbuf, 0, nbuf.Length);
			reflected.Process(rbuf, 0, rbuf.Length);

			int nOnsets = UpwardMeanCrossings(Envelope(nbuf));
			int rOnsets = UpwardMeanCrossings(Envelope(rbuf));
			Assert.True(rOnsets > nOnsets, $"reflected onsets {rOnsets} not faster than normal {nOnsets}");
		}

		// Count upward crossings of the signal's own mean: an onset proxy that rises with tempo.
		private static int UpwardMeanCrossings(double[] x) {
			double mean = Mean(x);
			int count = 0;
			for (int i = 1; i < x.Length; i++)
				if (x[i - 1] <= mean && x[i] > mean) count++;
			return count;
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
