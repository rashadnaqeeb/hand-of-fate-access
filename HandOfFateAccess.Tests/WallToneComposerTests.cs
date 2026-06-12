using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class WallToneComposerTests {
		[Fact]
		public void AtRangeEdge_Silent() {
			Assert.Equal(0f, WallToneComposer.TargetVolume(WallToneComposer.Range));
		}

		[Fact]
		public void BeyondRange_Silent() {
			Assert.Equal(0f, WallToneComposer.TargetVolume(WallToneComposer.Range + 5f));
		}

		[Fact]
		public void NoHit_Silent() {
			Assert.Equal(0f, WallToneComposer.TargetVolume(float.PositiveInfinity));
		}

		[Fact]
		public void NegativeDistance_Silent() {
			// A degenerate measurement must not slip through as a full-volume blast.
			Assert.Equal(0f, WallToneComposer.TargetVolume(-1f));
		}

		[Fact]
		public void JustInsideRange_NearlySilent() {
			float v = WallToneComposer.TargetVolume(WallToneComposer.Range - 0.01f);
			Assert.True(v > 0f && v < 0.01f);
		}

		[Fact]
		public void VolumeRisesLinearlyAsWallCloses() {
			Assert.Equal(WallToneComposer.MaxVolume * 0.5f, WallToneComposer.TargetVolume(WallToneComposer.Range / 2f), 3);
		}

		[Fact]
		public void AtPlayerPosition_PeaksAtMaxVolume() {
			Assert.Equal(WallToneComposer.MaxVolume, WallToneComposer.TargetVolume(0f), 3);
		}

		[Fact]
		public void NeverExceedsMaxVolume() {
			Assert.True(WallToneComposer.TargetVolume(0f) <= WallToneComposer.MaxVolume);
		}

		[Fact]
		public void SideWallsRestStronglyPanned_BeyondTheGate() {
			Assert.Equal(WallToneComposer.RestPan, WallToneComposer.PanFor(WallSide.Right, WallToneComposer.PanGateRange));
			Assert.Equal(WallToneComposer.RestPan, WallToneComposer.PanFor(WallSide.Right, WallToneComposer.Range));
			Assert.Equal(-WallToneComposer.RestPan, WallToneComposer.PanFor(WallSide.Left, float.PositiveInfinity));
		}

		[Fact]
		public void SideWallsSlideFullyInEar_AtContact() {
			Assert.Equal(1f, WallToneComposer.PanFor(WallSide.Right, 0f));
			Assert.Equal(-1f, WallToneComposer.PanFor(WallSide.Left, 0f));
		}

		[Fact]
		public void SidePanSlidesLinearlyInsideTheGate() {
			float midway = WallToneComposer.PanFor(WallSide.Right, WallToneComposer.PanGateRange / 2f);
			Assert.Equal((1f + WallToneComposer.RestPan) / 2f, midway, 3);
		}

		[Fact]
		public void SidePanNeverLeavesItsSide() {
			// The slide is into the ear only: at no distance does a side wind drift toward
			// center, where it would walk into the fore/aft pair's position.
			for (float d = 0f; d <= WallToneComposer.Range; d += 0.1f) {
				float pan = WallToneComposer.PanFor(WallSide.Right, d);
				Assert.InRange(pan, WallToneComposer.RestPan, 1f);
			}
		}

		[Fact]
		public void DegenerateDistance_RestsThePan() {
			// No wall (or a broken measurement) settles back to rest, never to hard pan.
			Assert.Equal(WallToneComposer.RestPan, WallToneComposer.PanFor(WallSide.Right, float.NaN));
			Assert.Equal(WallToneComposer.RestPan, WallToneComposer.PanFor(WallSide.Right, -1f));
		}

		[Fact]
		public void ForwardAndBackWallsAreCentred_AtAnyDistance() {
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Above, 0f));
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Above, float.PositiveInfinity));
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Below, 0f));
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Below, 1f));
		}

		[Fact]
		public void Pitch_AheadIsUnshifted_BehindIsTheFullSpanDown() {
			Assert.Equal(1f, WallToneComposer.PitchFor(WallSide.Above));
			Assert.Equal((float)System.Math.Pow(2.0, -WallToneComposer.PitchSpanOctaves),
				WallToneComposer.PitchFor(WallSide.Below), 4);
		}

		[Fact]
		public void Pitch_SideWindsSitHalfwayDown() {
			float halfway = (float)System.Math.Pow(2.0, -WallToneComposer.PitchSpanOctaves * 0.5);
			Assert.Equal(halfway, WallToneComposer.PitchFor(WallSide.Left), 4);
			Assert.Equal(WallToneComposer.PitchFor(WallSide.Left), WallToneComposer.PitchFor(WallSide.Right));
		}

		[Fact]
		public void Pitch_OrderedNorthDownToSouth_AndNeverAboveTheAuthoredClip() {
			// Souther is darker, and the shifter only ever works downward from the synth's
			// authored brightness, the rule every pitched sound follows.
			float above = WallToneComposer.PitchFor(WallSide.Above);
			float side = WallToneComposer.PitchFor(WallSide.Right);
			float below = WallToneComposer.PitchFor(WallSide.Below);
			Assert.True(above > side && side > below);
			Assert.True(above <= 1f);
			Assert.True(below >= HandOfFateAccess.Audio.SoundParams.MinPitch);
		}

		[Fact]
		public void Smooth_MovesTowardTarget_WithoutOvershooting() {
			float next = WallToneComposer.Smooth(0f, 1f, 1f / 60f);
			Assert.True(next > 0f && next < 1f);
		}

		[Fact]
		public void Smooth_NonPositiveDelta_HoldsValue() {
			Assert.Equal(0.3f, WallToneComposer.Smooth(0.3f, 1f, 0f));
			Assert.Equal(0.3f, WallToneComposer.Smooth(0.3f, 1f, -0.5f));
		}

		[Fact]
		public void Smooth_LargeDelta_ConvergesToTarget() {
			// A long frame closes essentially all of the gap.
			float next = WallToneComposer.Smooth(0f, 1f, 5f);
			Assert.Equal(1f, next, 2);
		}

		[Fact]
		public void Smooth_RepeatedSteps_ApproachTarget() {
			float v = 0f;
			for (int i = 0; i < 120; i++) // ~2 seconds at 60fps
				v = WallToneComposer.Smooth(v, 1f, 1f / 60f);
			Assert.True(v > 0.99f);
		}

		[Fact]
		public void Smooth_FallsTowardZero() {
			float next = WallToneComposer.Smooth(1f, 0f, 1f / 60f);
			Assert.True(next < 1f && next > 0f);
		}
	}
}
