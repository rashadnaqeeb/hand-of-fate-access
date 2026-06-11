using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class BeaconComposerTests {
		private static float MidPitch => ProjectileSonifier.PitchFor(0f);

		// --- Identity: the two beacons are distinct samples. ---

		[Fact]
		public void ChestAndExit_AreDistinctSamples() {
			Assert.NotEqual(BeaconComposer.ChestKey, BeaconComposer.ExitKey);
		}

		// --- Cadence: the gap after each ping is the distance readout, parking-sensor
		// style - eager underfoot, sparse far away - and starts when the sound ends. ---

		[Fact]
		public void NextPing_StartsTheDistanceGapAfterTheClipEnds() {
			Assert.Equal(10f + 1.3f + BeaconComposer.NearPingGap, BeaconComposer.NextPingTime(10f, 1.3f, 1f, 0f), 3);
		}

		[Fact]
		public void PitchedDownPing_RunsLonger_RepingsLater() {
			// Pitch is a playback-rate multiplier: at 0.5 the clip takes twice its authored
			// time, and the gap must follow the sound, not the file.
			Assert.Equal(2f + BeaconComposer.NearPingGap, BeaconComposer.NextPingTime(0f, 1f, 0.5f, 0f), 3);
			Assert.True(BeaconComposer.NextPingTime(0f, 1f, 0.8f, 5f) > BeaconComposer.NextPingTime(0f, 1f, 1f, 5f));
		}

		[Fact]
		public void NearerObject_PingsMoreOften() {
			Assert.True(BeaconComposer.GapFor(RangingCurve.FloorRange / 4f)
				< BeaconComposer.GapFor(RangingCurve.FloorRange / 2f));
		}

		[Fact]
		public void FarGap_HoldsAtAndBeyondTheFloorRange() {
			Assert.Equal(BeaconComposer.FarPingGap, BeaconComposer.GapFor(RangingCurve.FloorRange), 3);
			Assert.Equal(BeaconComposer.FarPingGap, BeaconComposer.GapFor(RangingCurve.FloorRange * 10f), 3);
		}

		[Fact]
		public void DegenerateDistance_ReadsFar_NotEager() {
			Assert.Equal(BeaconComposer.FarPingGap, BeaconComposer.GapFor(float.NaN), 3);
		}

		[Fact]
		public void ApproachAcrossTheRoom_IsAudibleAsCadence() {
			// The whole point: across a small room volume barely moves (it floors), so the
			// gap must change clearly between "across the room" and "a few steps away".
			float far = BeaconComposer.GapFor(25f);
			float near = BeaconComposer.GapFor(5f);
			Assert.True(far > near * 2f);
		}

		// --- Bearing: the shared grammar, pan east/west, down-biased pitch north/south. ---

		[Fact]
		public void DueNorth_Centered_BrightestUnshifted() {
			Assert.True(BeaconComposer.TryCompose(0f, 5f, out SoundParams sp, out _));
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(1f, sp.Pitch, 3);
		}

		[Fact]
		public void DueWest_FullLeft_MidPitch() {
			Assert.True(BeaconComposer.TryCompose(-5f, 0f, out SoundParams sp, out _));
			Assert.Equal(-1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void Northeast_HalfRight_AboveMid() {
			Assert.True(BeaconComposer.TryCompose(3f, 3f, out SoundParams sp, out _));
			Assert.Equal(0.5f, sp.Pan, 3);
			Assert.True(sp.Pitch > MidPitch && sp.Pitch <= 1f);
		}

		[Fact]
		public void SameBearing_SamePanAndPitch_AnyDistance() {
			Assert.True(BeaconComposer.TryCompose(-2f, 2f, out SoundParams near, out _));
			Assert.True(BeaconComposer.TryCompose(-20f, 20f, out SoundParams far, out _));
			Assert.Equal(near.Pan, far.Pan, 3);
			Assert.Equal(near.Pitch, far.Pitch, 3);
		}

		[Fact]
		public void Bearing_StaysWithinSeamClamps() {
			Assert.True(BeaconComposer.TryCompose(-100f, 3f, out SoundParams sp, out _));
			Assert.InRange(sp.Pan, -1f, 1f);
			Assert.InRange(sp.Pitch, SoundParams.MinPitch, SoundParams.MaxPitch);
		}

		[Fact]
		public void ReportedDistance_IsTheGroundDistance() {
			// The caller schedules the next ping from this: a 3-4-5 triangle reads 5.
			Assert.True(BeaconComposer.TryCompose(3f, 4f, out _, out float distance));
			Assert.Equal(5f, distance, 3);
		}

		// --- Reached: standing on the object suppresses the ping. ---

		[Fact]
		public void OnTheObject_Suppressed() {
			Assert.False(BeaconComposer.TryCompose(0f, 0f, out _, out _));
		}

		[Fact]
		public void WithinReachedRange_Suppressed() {
			Assert.False(BeaconComposer.TryCompose(BeaconComposer.ReachedRange * 0.7f, 0f, out _, out _));
		}

		[Fact]
		public void JustBeyondReachedRange_Pings() {
			Assert.True(BeaconComposer.TryCompose(BeaconComposer.ReachedRange * 1.2f, 0f, out _, out _));
		}

		// --- Volume: the shared ranging curve mapped onto the beacon's loudness identity,
		// floored, never silent while the object exists. ---

		[Fact]
		public void Closer_IsLouder() {
			float near = BeaconComposer.VolumeFor(RangingCurve.FloorRange / 4f);
			float far = BeaconComposer.VolumeFor(RangingCurve.FloorRange / 2f);
			Assert.True(near > far);
		}

		[Fact]
		public void WithinReach_FullVolume() {
			Assert.Equal(BeaconComposer.MaxVolume, BeaconComposer.VolumeFor(RangingCurve.FullRange), 3);
		}

		[Fact]
		public void AtTheFloorRange_FloorVolume() {
			Assert.Equal(BeaconComposer.MinVolume, BeaconComposer.VolumeFor(RangingCurve.FloorRange), 3);
		}

		[Fact]
		public void FarAcrossTheLevel_StillAudible() {
			// A beacon is a navigation target: silence must mean "no object", never "object
			// far away", so the floor holds at any distance.
			Assert.True(BeaconComposer.VolumeFor(RangingCurve.FloorRange * 10f) > 0f);
		}

		[Fact]
		public void RangesOnTheSharedCurve_LikeTheLocator() {
			// One loudness-to-distance grammar: at any distance the beacon sits at the same
			// closeness fraction of its own loudness band as the enemy ping does of its.
			float distance = 11f;
			float beaconFraction = (BeaconComposer.VolumeFor(distance) - BeaconComposer.MinVolume)
				/ (BeaconComposer.MaxVolume - BeaconComposer.MinVolume);
			float pingFraction = (EnemyPingComposer.VolumeFor(distance) - EnemyPingComposer.MinVolume)
				/ (EnemyPingComposer.MaxVolume - EnemyPingComposer.MinVolume);
			Assert.Equal(pingFraction, beaconFraction, 3);
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
