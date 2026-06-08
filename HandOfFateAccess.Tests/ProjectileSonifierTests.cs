using System;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ProjectileSonifierTests {
		// Pitch at the north (unshifted) and south (darkest) extremes, plus the east/west mid.
		private static float NorthPitch => ProjectileSonifier.PitchFor(1f);
		private static float SouthPitch => ProjectileSonifier.PitchFor(-1f);
		private static float MidPitch => ProjectileSonifier.PitchFor(0f);

		// --- Cardinal bearings: pitch is down-biased, brightest north, darkest south. ---

		[Fact]
		public void DueNorth_Centered_BrightestUnshifted() {
			SoundParams sp = ProjectileSonifier.Compose(0f, 5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(1f, sp.Pitch, 3);  // the authored sample, no shift
		}

		[Fact]
		public void DueSouth_Centered_Darkest() {
			SoundParams sp = ProjectileSonifier.Compose(0f, -5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal((float)Math.Pow(2.0, -ProjectileSonifier.DownOctaves), sp.Pitch, 3);
		}

		[Fact]
		public void DueWest_FullLeft_MidPitch() {
			SoundParams sp = ProjectileSonifier.Compose(-5f, 0f);
			Assert.Equal(-1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void DueEast_FullRight_MidPitch() {
			SoundParams sp = ProjectileSonifier.Compose(5f, 0f);
			Assert.Equal(1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void WestAndEast_SamePitch() {
			Assert.Equal(ProjectileSonifier.Compose(-5f, 0f).Pitch, ProjectileSonifier.Compose(5f, 0f).Pitch, 3);
		}

		// --- Pitch is monotonic in northness and never rises above the unshifted sample. ---

		[Fact]
		public void PitchRisesNorthward() {
			Assert.True(SouthPitch < ProjectileSonifier.PitchFor(-0.5f));
			Assert.True(ProjectileSonifier.PitchFor(-0.5f) < MidPitch);
			Assert.True(MidPitch < ProjectileSonifier.PitchFor(0.5f));
			Assert.True(ProjectileSonifier.PitchFor(0.5f) < NorthPitch);
		}

		[Fact]
		public void PitchNeverAboveUnshifted() {
			for (float d = -1f; d <= 1f; d += 0.25f)
				Assert.True(ProjectileSonifier.PitchFor(d) <= 1f);
		}

		// --- Diagonals: the unit budget splits the bearing evenly by angle. ---

		[Fact]
		public void Northwest_HalfLeft_AbovMid() {
			SoundParams sp = ProjectileSonifier.Compose(-3f, 3f);
			Assert.Equal(-0.5f, sp.Pan, 3);
			Assert.True(sp.Pitch > MidPitch && sp.Pitch < 1f);
		}

		[Fact]
		public void Southwest_HalfLeft_BelowMid() {
			SoundParams sp = ProjectileSonifier.Compose(-2f, -2f);
			Assert.Equal(-0.5f, sp.Pan, 3);
			Assert.True(sp.Pitch < MidPitch && sp.Pitch > SouthPitch);
		}

		// --- Bearing is distance-independent: only direction sets pan and pitch. ---

		[Fact]
		public void DueWest_HardLeft_WhetherNearOrFar() {
			Assert.Equal(-1f, ProjectileSonifier.Compose(-1f, 0f).Pan, 3);
			Assert.Equal(-1f, ProjectileSonifier.Compose(-50f, 0f).Pan, 3);
		}

		[Fact]
		public void SameBearing_SamePanAndPitch_AnyDistance() {
			SoundParams near = ProjectileSonifier.Compose(-1f, 1f);
			SoundParams far = ProjectileSonifier.Compose(-20f, 20f);
			Assert.Equal(near.Pan, far.Pan, 3);
			Assert.Equal(near.Pitch, far.Pitch, 3);
		}

		[Fact]
		public void OnPlayer_Centered_MidPitch() {
			SoundParams sp = ProjectileSonifier.Compose(0f, 0f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void Bearing_StaysWithinSeamClamps() {
			// Any direction must land inside the backend's valid pan and pitch ranges.
			SoundParams sp = ProjectileSonifier.Compose(-100f, 3f);
			Assert.InRange(sp.Pan, -1f, 1f);
			Assert.InRange(sp.Pitch, SoundParams.MinPitch, SoundParams.MaxPitch);
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

		// --- A combined corner: a far southwest projectile sits left, low, and faint. ---

		[Fact]
		public void FarSouthWest_LeftLowAndQuiet() {
			SoundParams sp = ProjectileSonifier.Compose(-ProjectileSonifier.FalloffRange, -ProjectileSonifier.FalloffRange);
			Assert.True(sp.Pan < 0f);
			Assert.True(sp.Pitch < 1f);
			Assert.Equal(ProjectileSonifier.MinVolume, sp.Volume, 3);
		}
	}
}
