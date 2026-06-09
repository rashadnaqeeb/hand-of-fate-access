using System;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ZoneSynthTests {
		private const int SampleRate = 44100;

		public static TheoryData<string, float[]> AllLoops() => new TheoryData<string, float[]> {
			{ ZoneSynth.ArmingKey, ZoneSynth.RenderArming(SampleRate) },
			{ ZoneSynth.ActiveKey, ZoneSynth.RenderActive(SampleRate) },
			{ ZoneSynth.InsideKey, ZoneSynth.RenderInside(SampleRate) },
		};

		[Theory]
		[MemberData(nameof(AllLoops))]
		public void Loop_IsExactlyOneSecond(string key, float[] loop) {
			Assert.NotNull(key);
			Assert.Equal(SampleRate, loop.Length);
		}

		[Theory]
		[MemberData(nameof(AllLoops))]
		public void Loop_PeaksUnderUnity(string key, float[] loop) {
			Assert.NotNull(key);
			float peak = 0f;
			for (int i = 0; i < loop.Length; i++) peak = Math.Max(peak, Math.Abs(loop[i]));
			Assert.InRange(peak, 0.1f, 1f);
		}

		[Theory]
		[MemberData(nameof(AllLoops))]
		public void Loop_WrapsWithoutClick(string key, float[] loop) {
			Assert.NotNull(key);
			// The pulse envelope is zero at every pulse boundary including the loop wrap, so
			// both ends sit at silence and the seam cannot click.
			Assert.InRange(Math.Abs(loop[0]), 0f, 0.01f);
			Assert.InRange(Math.Abs(loop[loop.Length - 1]), 0f, 0.01f);
		}

		[Fact]
		public void States_AreDistinctSignals() {
			float[] arming = ZoneSynth.RenderArming(SampleRate);
			float[] active = ZoneSynth.RenderActive(SampleRate);
			float[] inside = ZoneSynth.RenderInside(SampleRate);
			Assert.NotEqual(PulseCount(arming), PulseCount(active));
			Assert.NotEqual(PulseCount(active), PulseCount(inside));
		}

		[Fact]
		public void PulseRates_MatchTheConstants() {
			Assert.Equal(ZoneSynth.ArmingPulseHz, PulseCount(ZoneSynth.RenderArming(SampleRate)));
			Assert.Equal(ZoneSynth.ActivePulseHz, PulseCount(ZoneSynth.RenderActive(SampleRate)));
			Assert.Equal(ZoneSynth.InsidePulseHz, PulseCount(ZoneSynth.RenderInside(SampleRate)));
		}

		[Fact]
		public void PulseRates_StaySeparableUnderTheBearingPitchShift() {
			// The bearing pitch-shift slows a loop by up to the projectile grammar's full
			// south deflection. The slowest (southern) faster state must still pulse faster
			// than the fastest (northern) slower state, or a shifted active zone could read
			// as an arming one.
			float slowest = (float)Math.Pow(2.0, -ProjectileSonifier.DownOctaves);
			Assert.True(ZoneSynth.ActivePulseHz * slowest > ZoneSynth.ArmingPulseHz);
			Assert.True(ZoneSynth.InsidePulseHz * slowest > ZoneSynth.ActivePulseHz);
		}

		// Pulses in the loop, counted as rises of the amplitude envelope across a threshold.
		// The envelope is |sin|^k per pulse, so each pulse crosses once up and once down.
		private static int PulseCount(float[] loop) {
			const int window = 256;
			const float threshold = 0.05f;
			int rises = 0;
			bool above = false;
			for (int start = 0; start + window <= loop.Length; start += window) {
				float peak = 0f;
				for (int i = start; i < start + window; i++) peak = Math.Max(peak, Math.Abs(loop[i]));
				bool nowAbove = peak > threshold;
				if (nowAbove && !above) rises++;
				above = nowAbove;
			}
			return rises;
		}
	}
}
