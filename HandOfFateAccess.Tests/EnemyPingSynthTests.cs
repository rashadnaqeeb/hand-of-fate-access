using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class EnemyPingSynthTests {
		private const int SampleRate = 44100;
		private static readonly float[] Ping = EnemyPingSynth.Render(SampleRate);

		[Fact]
		public void Ping_HasTheAuthoredDuration() {
			Assert.Equal((int)(SampleRate * EnemyPingSynth.DurationSeconds), Ping.Length);
		}

		[Fact]
		public void Ping_PeaksUnderUnity() {
			float peak = 0f;
			for (int i = 0; i < Ping.Length; i++) peak = Math.Max(peak, Math.Abs(Ping[i]));
			Assert.InRange(peak, 0.1f, 1f);
		}

		[Fact]
		public void Ping_StartsAndEndsSilent() {
			// Attack ramps from zero and the fade-out forces the last sample to zero, so a
			// one-shot can never click on or off.
			Assert.InRange(Math.Abs(Ping[0]), 0f, 0.01f);
			Assert.InRange(Math.Abs(Ping[Ping.Length - 1]), 0f, 0.01f);
		}

		[Fact]
		public void Ping_PeaksEarlyThenDecays() {
			// The beep's character: all the energy up front (peak inside the first 30 ms),
			// a fast decay, and only a faint ring left past 100 ms.
			int peakAt = 0;
			float peak = 0f;
			for (int i = 0; i < Ping.Length; i++) {
				float a = Math.Abs(Ping[i]);
				if (a > peak) { peak = a; peakAt = i; }
			}
			Assert.InRange(peakAt, 0, (int)(0.030f * SampleRate));

			float body = Rms(0f, 0.040f);
			float ring = Rms(0.100f, 0.150f);
			Assert.True(ring < body / 8f, "ring " + ring + " not well below body " + body);
			Assert.True(ring > 0.001f, "ring is silent; the sample's faint tail is missing");
		}

		[Fact]
		public void Ping_IsOddHarmonicLike() {
			// The square-wave family signature copied from the reference beep: the third
			// harmonic carries far more energy than the (absent) second.
			double third = Goertzel(414f * 3f);
			double second = Goertzel(414f * 2f);
			Assert.True(third > second * 10.0,
				"3rd harmonic " + third + " does not dominate 2nd " + second);
		}

		private static float Rms(float fromSeconds, float toSeconds) {
			int from = (int)(fromSeconds * SampleRate);
			int to = Math.Min((int)(toSeconds * SampleRate), Ping.Length);
			double sum = 0.0;
			for (int i = from; i < to; i++) sum += Ping[i] * Ping[i];
			return (float)Math.Sqrt(sum / (to - from));
		}

		// Power at one frequency over the ping's body (Goertzel filter).
		private static double Goertzel(float frequency) {
			int count = (int)(0.080f * SampleRate);
			double k = 2.0 * Math.Cos(2.0 * Math.PI * frequency / SampleRate);
			double s1 = 0.0, s2 = 0.0;
			for (int i = 0; i < count; i++) {
				double s = Ping[i] + k * s1 - s2;
				s2 = s1;
				s1 = s;
			}
			return s1 * s1 + s2 * s2 - k * s1 * s2;
		}
	}
}
