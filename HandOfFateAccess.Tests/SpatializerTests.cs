using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class SpatializerTests {
		private const int Rate = 44100;

		// --- Bearing: pure bearing past the crossover, absolute offset inside it. ---

		[Fact]
		public void Bearing_DueEastFar_HardRight_NoDepth() {
			Spatializer.Bearing(10f, 0f, out float pan, out float depth);
			Assert.Equal(1f, pan, 3);
			Assert.Equal(0f, depth, 3);
		}

		[Fact]
		public void Bearing_DueNorthFar_Centered_FullAhead() {
			Spatializer.Bearing(0f, 10f, out float pan, out float depth);
			Assert.Equal(0f, pan, 3);
			Assert.Equal(1f, depth, 3);
		}

		[Fact]
		public void Bearing_DueSouthFar_FullBehind() {
			Spatializer.Bearing(0f, -10f, out _, out float depth);
			Assert.Equal(-1f, depth, 3);
		}

		[Fact]
		public void Bearing_Diagonal_SplitsByAngle() {
			Spatializer.Bearing(10f, 10f, out float pan, out float depth);
			Assert.Equal(0.707f, pan, 3);
			Assert.Equal(0.707f, depth, 3);
		}

		[Fact]
		public void Bearing_InsideCrossover_TracksAbsoluteOffset() {
			// One unit east, essentially on the listener: a fraction of the field, not
			// hard right, so a source passing close glides instead of snapping ear to ear.
			Spatializer.Bearing(1f, 0f, out float pan, out _);
			Assert.Equal(1f / Spatializer.PanWidth, pan, 3);
		}

		[Fact]
		public void Bearing_PanIsContinuousAcrossTheCrossover() {
			Spatializer.Bearing(Spatializer.PanWidth - 0.01f, 0f, out float justInside, out _);
			Spatializer.Bearing(Spatializer.PanWidth + 0.01f, 0f, out float justOutside, out _);
			Assert.Equal(justInside, justOutside, 2);
		}

		[Fact]
		public void Bearing_OnTheListener_CenteredAndDry() {
			Spatializer.Bearing(0f, 0f, out float pan, out float depth);
			Assert.Equal(0f, pan);
			Assert.Equal(0f, depth);
		}

		// --- Cue: equal-power gains, the ITD riding the same lateral fraction, and the
		// rear muffle only in the southern hemisphere. ---

		[Fact]
		public void Cue_Centered_EqualPowerBothEars() {
			SpatialCue cue = Spatializer.Cue(new SoundParams(0f, 1f, 1f), Rate);
			Assert.Equal(0.7071f, cue.GainLeft, 3);
			Assert.Equal(0.7071f, cue.GainRight, 3);
			Assert.Equal(0f, cue.ItdSamples, 3);
		}

		[Fact]
		public void Cue_HardLeft_SilencesTheRightEar() {
			SpatialCue cue = Spatializer.Cue(new SoundParams(-1f, 1f, 1f), Rate);
			Assert.Equal(1f, cue.GainLeft, 3);
			Assert.Equal(0f, cue.GainRight, 3);
		}

		[Fact]
		public void Cue_VolumeScalesBothGains() {
			SpatialCue full = Spatializer.Cue(new SoundParams(0.3f, 1f, 1f), Rate);
			SpatialCue half = Spatializer.Cue(new SoundParams(0.3f, 1f, 0.5f), Rate);
			Assert.Equal(full.GainLeft * 0.5f, half.GainLeft, 3);
			Assert.Equal(full.GainRight * 0.5f, half.GainRight, 3);
		}

		[Fact]
		public void Cue_ItdRidesThePanFraction() {
			// The delay and the level cue must always agree: half the field, half the delay.
			SpatialCue full = Spatializer.Cue(new SoundParams(1f, 1f, 1f), Rate);
			SpatialCue half = Spatializer.Cue(new SoundParams(0.5f, 1f, 1f), Rate);
			Assert.Equal(Spatializer.MaxItdSeconds * Rate, full.ItdSamples, 3);
			Assert.Equal(full.ItdSamples * 0.5f, half.ItdSamples, 3);
		}

		[Fact]
		public void Cue_WestIsANegativeDelay() {
			// Sign is the source side: negative means the RIGHT ear lags (source west).
			SpatialCue cue = Spatializer.Cue(new SoundParams(-0.5f, 1f, 1f), Rate);
			Assert.True(cue.ItdSamples < 0f);
		}

		[Fact]
		public void Cue_AheadAndBeside_AreDry() {
			SpatialCue ahead = Spatializer.Cue(new SoundParams(0f, 1f, 1f, 1f), Rate);
			SpatialCue beside = Spatializer.Cue(new SoundParams(1f, 0f, 1f, 1f), Rate);
			Assert.Equal(0f, ahead.WetMix, 3);
			Assert.Equal(0f, beside.WetMix, 3);
			Assert.Equal(Spatializer.OpenHz, ahead.CutoffHz, 0);
		}

		[Fact]
		public void Cue_DeadBehind_MuffledAndMixed() {
			SpatialCue cue = Spatializer.Cue(new SoundParams(0f, -1f, 1f, 1f), Rate);
			Assert.Equal(Spatializer.MuffledHz, cue.CutoffHz, 0);
			Assert.Equal(Spatializer.MaxWet, cue.WetMix, 3);
		}

		[Fact]
		public void Cue_WetMixNeverReachesOne() {
			// The dry remainder is the guarantee that a bright, narrowband cue can never
			// filter into silence behind the player.
			SpatialCue cue = Spatializer.Cue(new SoundParams(0f, -1f, 1f, 1f), Rate);
			Assert.True(cue.WetMix < 1f);
		}

		[Fact]
		public void Cue_MuffleDeepensEvenlyInOctaves() {
			// Log-space interpolation: each step back drops the cutoff by a constant RATIO.
			SpatialCue quarter = Spatializer.Cue(new SoundParams(0f, -0.25f, 1f, 1f), Rate);
			SpatialCue half = Spatializer.Cue(new SoundParams(0f, -0.5f, 1f, 1f), Rate);
			SpatialCue threeQ = Spatializer.Cue(new SoundParams(0f, -0.75f, 1f, 1f), Rate);
			Assert.Equal(half.CutoffHz / quarter.CutoffHz, threeQ.CutoffHz / half.CutoffHz, 2);
			Assert.True(threeQ.CutoffHz < half.CutoffHz && half.CutoffHz < quarter.CutoffHz);
		}

		[Fact]
		public void Cue_ClampsWildInput() {
			// The far gain at a hard pan is cos(pi/2), a sub-epsilon negative in float:
			// inaudible, so the bounds allow it.
			SpatialCue cue = Spatializer.Cue(new SoundParams(5f, -9f, 1f, 3f), Rate);
			Assert.True(cue.GainLeft >= -1e-6f && cue.GainLeft <= 1f);
			Assert.True(cue.GainRight >= -1e-6f && cue.GainRight <= 1f);
			Assert.True(Math.Abs(cue.ItdSamples) <= Spatializer.MaxItdSeconds * Rate + 0.001f);
			Assert.Equal(Spatializer.MaxWet, cue.WetMix, 3);
		}
	}
}
