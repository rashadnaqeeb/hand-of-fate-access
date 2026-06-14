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
		public void SideWallsAreHardPanned() {
			// Bearing only: a wall's bearing is fully lateral, so the side winds sit hard in
			// their ear, no distance term.
			Assert.Equal(1f, WallToneComposer.PanFor(WallSide.Right));
			Assert.Equal(-1f, WallToneComposer.PanFor(WallSide.Left));
		}

		[Fact]
		public void ForwardAndBackWallsAreCentred() {
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Above));
			Assert.Equal(0f, WallToneComposer.PanFor(WallSide.Below));
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
