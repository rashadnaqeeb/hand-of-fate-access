using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ProjectileSonifierTests {
		// --- Cardinal bearings: pan is east/west, depth is fore/aft, the sample plays
		// unshifted everywhere (pitch 1: position never modulates the tumble). ---

		[Fact]
		public void DueNorth_Centered_FullAhead() {
			SoundParams sp = ProjectileSonifier.Compose(0f, 5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(1f, sp.Depth, 3);
			Assert.Equal(1f, sp.Pitch, 3);
		}

		[Fact]
		public void DueSouth_Centered_FullBehind() {
			SoundParams sp = ProjectileSonifier.Compose(0f, -5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(-1f, sp.Depth, 3);
			Assert.Equal(1f, sp.Pitch, 3);
		}

		[Fact]
		public void DueWest_FullLeft_NoDepth() {
			SoundParams sp = ProjectileSonifier.Compose(-5f, 0f);
			Assert.Equal(-1f, sp.Pan, 3);
			Assert.Equal(0f, sp.Depth, 3);
		}

		[Fact]
		public void DueEast_FullRight_NoDepth() {
			SoundParams sp = ProjectileSonifier.Compose(5f, 0f);
			Assert.Equal(1f, sp.Pan, 3);
			Assert.Equal(0f, sp.Depth, 3);
		}

		// --- Diagonals: pan and depth are the bearing's sine and cosine, so a diagonal
		// sits evenly between the cardinals. ---

		[Fact]
		public void Northwest_HalfField_HalfAhead() {
			SoundParams sp = ProjectileSonifier.Compose(-5f, 5f);
			Assert.Equal(-0.707f, sp.Pan, 3);
			Assert.Equal(0.707f, sp.Depth, 3);
		}

		[Fact]
		public void Southwest_HalfField_HalfBehind() {
			SoundParams sp = ProjectileSonifier.Compose(-5f, -5f);
			Assert.Equal(-0.707f, sp.Pan, 3);
			Assert.Equal(-0.707f, sp.Depth, 3);
		}

		// --- Past the crossover the bearing is distance-independent; inside it the pan
		// tracks the absolute offset, so a passing projectile glides across the field
		// instead of snapping ear to ear. ---

		[Fact]
		public void SameBearing_SamePlacement_AnyDistancePastCrossover() {
			SoundParams near = ProjectileSonifier.Compose(-4f, 4f);
			SoundParams far = ProjectileSonifier.Compose(-20f, 20f);
			Assert.Equal(near.Pan, far.Pan, 3);
			Assert.Equal(near.Depth, far.Depth, 3);
		}

		[Fact]
		public void InsideCrossover_PanSoftensWithOffset() {
			// One unit to the west, essentially on the player: not hard left.
			SoundParams sp = ProjectileSonifier.Compose(-1f, 0f);
			Assert.Equal(-1f / Spatializer.PanWidth, sp.Pan, 3);
			Assert.True(sp.Pan > -1f);
		}

		[Fact]
		public void OnPlayer_CenteredAndDry() {
			SoundParams sp = ProjectileSonifier.Compose(0f, 0f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(0f, sp.Depth, 3);
		}

		[Fact]
		public void Bearing_StaysWithinSeamClamps() {
			// Any direction must land inside the voice chain's valid ranges.
			SoundParams sp = ProjectileSonifier.Compose(-100f, 3f);
			Assert.InRange(sp.Pan, -1f, 1f);
			Assert.InRange(sp.Depth, -1f, 1f);
		}

		// --- Volume: ground distance maps to loudness, peak near, floor far. ---

		[Fact]
		public void OnPlayer_LoudestVolume() {
			Assert.Equal(ProjectileSonifier.MaxVolume, ProjectileSonifier.VolumeFor(0f), 3);
		}

		[Fact]
		public void AtFalloffRange_FloorVolume() {
			Assert.Equal(ProjectileSonifier.MinVolume, ProjectileSonifier.VolumeFor(ProjectileSonifier.FalloffRange), 3);
		}

		[Fact]
		public void BeyondFalloffRange_HoldsFloor() {
			Assert.Equal(ProjectileSonifier.MinVolume, ProjectileSonifier.VolumeFor(ProjectileSonifier.FalloffRange * 3f), 3);
		}

		[Fact]
		public void Closer_IsLouder() {
			float near = ProjectileSonifier.VolumeFor(ProjectileSonifier.FalloffRange / 4f);
			float far = ProjectileSonifier.VolumeFor(ProjectileSonifier.FalloffRange / 2f);
			Assert.True(near > far);
		}

		[Fact]
		public void ActiveProjectile_NeverSilent() {
			// A live projectile is a threat the player must keep tracking, so even at the
			// far floor its volume stays above zero.
			Assert.True(ProjectileSonifier.VolumeFor(ProjectileSonifier.FalloffRange * 10f) > 0f);
		}

		[Fact]
		public void DegenerateDistance_DropsToFloor_NotABlast() {
			Assert.Equal(ProjectileSonifier.MinVolume, ProjectileSonifier.VolumeFor(float.NaN), 3);
		}

		// --- A combined corner: a far southwest projectile sits left, behind, and faint. ---

		[Fact]
		public void FarSouthWest_LeftBehindAndQuiet() {
			SoundParams sp = ProjectileSonifier.Compose(-ProjectileSonifier.FalloffRange, -ProjectileSonifier.FalloffRange);
			Assert.True(sp.Pan < 0f);
			Assert.True(sp.Depth < 0f);
			Assert.Equal(ProjectileSonifier.MinVolume, sp.Volume, 3);
		}
	}
}
