using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ZoneSonifierTests {
		[Fact]
		public void OutsideZone_SoundSitsTowardIt() {
			// Zone center 6 east, radius 2: nearest edge is due east, so the voice pans
			// right and fleeing it (west) is the correct move.
			ZoneCue cue = ZoneSonifier.Compose(right: 6f, forward: 0f, outerRadius: 2f, innerRadius: 0f, ZonePhase.Active);
			Assert.True(cue.Audible);
			Assert.False(cue.Inside);
			Assert.Equal(ZoneSynth.ActiveKey, cue.ClipKey);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(4f, cue.Distance, 3);
		}

		[Fact]
		public void Volume_FallsWithGap_AndSilencesAtFalloff() {
			ZoneCue near = ZoneSonifier.Compose(3f, 0f, 2f, 0f, ZonePhase.Active);
			ZoneCue far = ZoneSonifier.Compose(8f, 0f, 2f, 0f, ZonePhase.Active);
			Assert.True(near.Params.Volume > far.Params.Volume);

			ZoneCue gone = ZoneSonifier.Compose(2f + ZoneSonifier.FalloffRange, 0f, 2f, 0f, ZonePhase.Active);
			Assert.False(gone.Audible);
		}

		[Fact]
		public void InsideZone_RattlesAtFullVolume_TowardTheCenter() {
			// Player 1 unit west of a radius-3 zone's center: inside, full volume, and the
			// sound sits toward the center (east) so fleeing it exits through the near edge.
			ZoneCue cue = ZoneSonifier.Compose(1f, 0f, 3f, 0f, ZonePhase.Active);
			Assert.True(cue.Inside);
			Assert.Equal(ZoneSynth.InsideKey, cue.ClipKey);
			Assert.Equal(1f, cue.Params.Volume, 3);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(0f, cue.Distance, 3);
		}

		[Fact]
		public void InsideWhileArming_StillRattles() {
			// Standing in an arming zone: the verb is the same (get out, and it is free), so
			// the inside rattle overrides the soft arming throb.
			ZoneCue cue = ZoneSonifier.Compose(0.5f, 0f, 3f, 0f, ZonePhase.Arming);
			Assert.Equal(ZoneSynth.InsideKey, cue.ClipKey);
		}

		[Fact]
		public void ArmingOutside_UsesTheArmingLoop() {
			ZoneCue cue = ZoneSonifier.Compose(5f, 0f, 2f, 0f, ZonePhase.Arming);
			Assert.Equal(ZoneSynth.ArmingKey, cue.ClipKey);
		}

		[Fact]
		public void RingHole_IsSafe_AndTheBearingFlips() {
			// Ring with inner radius 4, outer 8; player 2 east of center sits in the safe
			// hole. The nearest band is west of them, so the voice pans left and fleeing it
			// (east, deeper into the hole) is the safe move.
			ZoneCue cue = ZoneSonifier.Compose(2f, 0f, 8f, 4f, ZonePhase.Active);
			Assert.False(cue.Inside);
			Assert.Equal(2f, cue.Distance, 3);
			Assert.True(cue.Params.Pan < -0.9f);
		}

		[Fact]
		public void OnTheCenter_NoBearing_CenteredAtMidPitch() {
			ZoneCue cue = ZoneSonifier.Compose(0f, 0f, 3f, 0f, ZonePhase.Active);
			Assert.True(cue.Inside);
			Assert.Equal(0f, cue.Params.Pan, 3);
			Assert.Equal(ProjectileSonifier.PitchFor(0f), cue.Params.Pitch, 3);
		}

		[Fact]
		public void Bearing_UsesTheSharedNorthSouthGrammar() {
			ZoneCue north = ZoneSonifier.Compose(0f, 6f, 2f, 0f, ZonePhase.Active);
			ZoneCue south = ZoneSonifier.Compose(0f, -6f, 2f, 0f, ZonePhase.Active);
			Assert.True(north.Params.Pitch > south.Params.Pitch);
			Assert.Equal(0f, north.Params.Pan, 3);
		}
	}
}
