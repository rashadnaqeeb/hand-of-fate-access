using System;
using HandOfFateAccess.Gambit;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class GambitVoiceDspTests {
		// Renders into a stereo block and returns (left, right, pos, finished).
		private static (float[] l, float[] r, double pos, bool fin) RenderStereo(
				float[] buffer, double pos, double step, bool loop, float pan, float vol, int frames) {
			var output = new float[frames * 2];
			double newPos = GambitVoiceDsp.Fill(buffer, pos, step, loop, pan, vol, output, 2, out bool fin);
			var l = new float[frames];
			var r = new float[frames];
			for (int i = 0; i < frames; i++) { l[i] = output[2 * i]; r[i] = output[2 * i + 1]; }
			return (l, r, newPos, fin);
		}

		[Fact]
		public void HardLeft_PutsAllEnergyInLeft() {
			float[] buf = { 0.5f, 0.5f, 0.5f, 0.5f };
			var (l, r, _, _) = RenderStereo(buf, 0, 1.0, loop: false, pan: -1f, vol: 1f, frames: 4);
			for (int i = 0; i < 4; i++) {
				Assert.Equal(0.5f, l[i], 4);
				Assert.Equal(0f, r[i], 4);
			}
		}

		[Fact]
		public void Centre_IsEqualPowerMinus3dB() {
			float[] buf = { 1f, 1f };
			var (l, r, _, _) = RenderStereo(buf, 0, 1.0, loop: false, pan: 0f, vol: 1f, frames: 2);
			float expected = (float)Math.Cos(Math.PI / 4);   // ~0.707
			Assert.Equal(expected, l[0], 3);
			Assert.Equal(expected, r[0], 3);
		}

		[Fact]
		public void OneShot_StopsAndZeroFillsAtEnd() {
			float[] buf = { 0.5f, 0.6f };
			var (l, _, pos, fin) = RenderStereo(buf, 0, 1.0, loop: false, pan: -1f, vol: 1f, frames: 4);
			Assert.Equal(0.5f, l[0], 4);
			Assert.Equal(0.6f, l[1], 4);
			Assert.Equal(0f, l[2], 4);     // past the end
			Assert.Equal(0f, l[3], 4);
			Assert.True(fin);
		}

		[Fact]
		public void Loop_WrapsBackToStart() {
			float[] buf = { 0.1f, 0.2f, 0.3f };
			var (l, _, _, fin) = RenderStereo(buf, 0, 1.0, loop: true, pan: -1f, vol: 1f, frames: 5);
			Assert.Equal(0.1f, l[0], 4);
			Assert.Equal(0.2f, l[1], 4);
			Assert.Equal(0.3f, l[2], 4);
			Assert.Equal(0.1f, l[3], 4);   // wrapped
			Assert.Equal(0.2f, l[4], 4);
			Assert.False(fin);             // a loop never finishes
		}

		[Fact]
		public void Resample_HalfStepInterpolatesMidpoints() {
			float[] buf = { 0f, 1f };
			// step 0.5: positions 0, 0.5, 1.0 -> 0, 0.5 (interp), then 1.0 is the last sample.
			var (l, _, _, _) = RenderStereo(buf, 0, 0.5, loop: false, pan: -1f, vol: 1f, frames: 3);
			Assert.Equal(0f, l[0], 4);
			Assert.Equal(0.5f, l[1], 4);   // midpoint between 0 and 1
			Assert.Equal(1f, l[2], 4);
		}

		[Theory]
		[InlineData(0.0)]
		[InlineData(-1.0)]
		[InlineData(double.NaN)]
		public void BadStep_FinishesSilentlyWithoutReading(double step) {
			float[] buf = { 0.5f, -0.5f, 0.5f };
			var output = new float[8];
			GambitVoiceDsp.Fill(buf, 0, step, true, -1f, 1f, output, 2, out bool fin);
			Assert.True(fin);
			foreach (float s in output) Assert.Equal(0f, s, 6);
		}

		[Fact]
		public void EmptyBuffer_FinishesSilently() {
			var output = new float[8];
			GambitVoiceDsp.Fill(new float[0], 0, 1.0, true, -1f, 1f, output, 2, out bool fin);
			Assert.True(fin);
			foreach (float s in output) Assert.Equal(0f, s, 6);
		}
	}
}
