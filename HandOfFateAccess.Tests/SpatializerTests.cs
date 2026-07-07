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

		// --- Cue: equal-power gains, the ITD riding the same lateral fraction. ---

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
		public void Cue_ClampsWildInput() {
			// The far gain at a hard pan is cos(pi/2), a sub-epsilon negative in float:
			// inaudible, so the bounds allow it.
			SpatialCue cue = Spatializer.Cue(new SoundParams(5f, 1f, 3f), Rate);
			Assert.True(cue.GainLeft >= -1e-6f && cue.GainLeft <= 1f);
			Assert.True(cue.GainRight >= -1e-6f && cue.GainRight <= 1f);
			Assert.True(Math.Abs(cue.ItdSamples) <= Spatializer.MaxItdSeconds * Rate + 0.001f);
		}

		// --- The fore/aft pitch law: the authored sound dead ahead, down-biased toward
		// the rear, never shifted up. ---

		[Fact]
		public void PitchFor_DeadAhead_IsTheAuthoredSound() {
			Assert.Equal(1f, Spatializer.PitchFor(1f), 4);
		}

		[Fact]
		public void PitchFor_DeadBehind_DropsTheFullSpan() {
			Assert.Equal((float)Math.Pow(2.0, -Spatializer.DownOctaves), Spatializer.PitchFor(-1f), 4);
		}

		[Fact]
		public void PitchFor_SideLine_SitsHalfwayDown() {
			Assert.Equal((float)Math.Pow(2.0, -Spatializer.DownOctaves * 0.5), Spatializer.PitchFor(0f), 4);
		}

		[Fact]
		public void PitchFor_NeverShiftsUp() {
			// Up-shifting smears a sample's transients into a metallic buzz; the whole
			// span works in the clean downward direction.
			for (float depth = -1f; depth <= 1f; depth += 0.125f)
				Assert.True(Spatializer.PitchFor(depth) <= 1f);
		}

		[Fact]
		public void PitchFor_DeepensEvenlyInOctaves() {
			// Each step back drops the pitch by a constant RATIO, so the darkening reads
			// evenly by ear across the whole fore/aft span.
			float ahead = Spatializer.PitchFor(0.5f);
			float mid = Spatializer.PitchFor(0f);
			float back = Spatializer.PitchFor(-0.5f);
			Assert.Equal(mid / ahead, back / mid, 4);
			Assert.True(back < mid && mid < ahead);
		}

		// --- Place: the one composition every positioned sound uses. ---

		[Fact]
		public void Place_FoldsTheBearingPitchIn() {
			SoundParams north = Spatializer.Place(0f, 10f, 1f);
			SoundParams west = Spatializer.Place(-10f, 0f, 1f);
			SoundParams south = Spatializer.Place(0f, -10f, 1f);
			Assert.Equal(1f, north.Pitch, 3);
			Assert.Equal(Spatializer.PitchFor(0f), west.Pitch, 3);
			Assert.Equal(Spatializer.PitchFor(-1f), south.Pitch, 3);
		}

		[Fact]
		public void Place_OnTheListener_CenteredAtMidPitch() {
			// No bearing: the source is everywhere, any direction away works.
			SoundParams sp = Spatializer.Place(0f, 0f, 1f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(Spatializer.PitchFor(0f), sp.Pitch, 3);
		}
	}
}
