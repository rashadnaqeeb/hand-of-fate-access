using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class BeaconComposerTests {
		private static float MidPitch => ProjectileSonifier.PitchFor(0f);

		// --- Identity: the two beacons are distinct samples on interleaved cadences. ---

		[Fact]
		public void ChestAndExit_AreDistinctSamples() {
			Assert.NotEqual(BeaconComposer.ChestKey, BeaconComposer.ExitKey);
		}

		[Fact]
		public void ExitCadence_SitsHalfACycleBehindTheChests() {
			Assert.Equal(BeaconComposer.PingInterval * 0.5f, BeaconComposer.ExitPhaseOffset, 3);
		}

		// --- Bearing: the shared grammar, pan east/west, down-biased pitch north/south. ---

		[Fact]
		public void DueNorth_Centered_BrightestUnshifted() {
			Assert.True(BeaconComposer.TryCompose(0f, 5f, out SoundParams sp));
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(1f, sp.Pitch, 3);
		}

		[Fact]
		public void DueWest_FullLeft_MidPitch() {
			Assert.True(BeaconComposer.TryCompose(-5f, 0f, out SoundParams sp));
			Assert.Equal(-1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void Northeast_HalfRight_AboveMid() {
			Assert.True(BeaconComposer.TryCompose(3f, 3f, out SoundParams sp));
			Assert.Equal(0.5f, sp.Pan, 3);
			Assert.True(sp.Pitch > MidPitch && sp.Pitch <= 1f);
		}

		[Fact]
		public void SameBearing_SamePanAndPitch_AnyDistance() {
			Assert.True(BeaconComposer.TryCompose(-2f, 2f, out SoundParams near));
			Assert.True(BeaconComposer.TryCompose(-20f, 20f, out SoundParams far));
			Assert.Equal(near.Pan, far.Pan, 3);
			Assert.Equal(near.Pitch, far.Pitch, 3);
		}

		[Fact]
		public void Bearing_StaysWithinSeamClamps() {
			Assert.True(BeaconComposer.TryCompose(-100f, 3f, out SoundParams sp));
			Assert.InRange(sp.Pan, -1f, 1f);
			Assert.InRange(sp.Pitch, SoundParams.MinPitch, SoundParams.MaxPitch);
		}

		// --- Reached: standing on the object suppresses the ping. ---

		[Fact]
		public void OnTheObject_Suppressed() {
			Assert.False(BeaconComposer.TryCompose(0f, 0f, out _));
		}

		[Fact]
		public void WithinReachedRange_Suppressed() {
			Assert.False(BeaconComposer.TryCompose(BeaconComposer.ReachedRange * 0.7f, 0f, out _));
		}

		[Fact]
		public void JustBeyondReachedRange_Pings() {
			Assert.True(BeaconComposer.TryCompose(BeaconComposer.ReachedRange * 1.2f, 0f, out _));
		}

		// --- Volume: distance-graded but floored, never silent while the object exists. ---

		[Fact]
		public void Closer_IsLouder() {
			float near = BeaconComposer.VolumeFor(BeaconComposer.FalloffRange / 4f);
			float far = BeaconComposer.VolumeFor(BeaconComposer.FalloffRange / 2f);
			Assert.True(near > far);
		}

		[Fact]
		public void AtFalloffRange_FloorVolume() {
			Assert.Equal(BeaconComposer.MinVolume, BeaconComposer.VolumeFor(BeaconComposer.FalloffRange), 3);
		}

		[Fact]
		public void FarAcrossTheLevel_StillAudible() {
			// A beacon is a navigation target: silence must mean "no object", never "object
			// far away", so the floor holds at any distance.
			Assert.True(BeaconComposer.VolumeFor(BeaconComposer.FalloffRange * 10f) > 0f);
		}

		[Fact]
		public void DegenerateDistance_DropsToFloor_NotABlast() {
			Assert.Equal(BeaconComposer.MinVolume, BeaconComposer.VolumeFor(float.NaN), 3);
		}

		[Fact]
		public void Guidance_SitsUnderTheTelegraphAlert() {
			// The beacon informs, the telegraph demands a reaction: the beacon must never be
			// the louder of the two at like distance.
			Assert.True(BeaconComposer.MaxVolume < AttackCueComposer.MaxVolume);
			Assert.True(BeaconComposer.MinVolume < AttackCueComposer.MinVolume);
		}
	}
}
