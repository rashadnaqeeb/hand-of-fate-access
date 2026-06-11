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
			ZoneCue near = ZoneSonifier.Compose(6f, 0f, 2f, 0f, ZonePhase.Active);
			ZoneCue far = ZoneSonifier.Compose(12f, 0f, 2f, 0f, ZonePhase.Active);
			Assert.True(near.Params.Volume > far.Params.Volume);

			ZoneCue gone = ZoneSonifier.Compose(2f + ZoneSonifier.FalloffRange, 0f, 2f, 0f, ZonePhase.Active);
			Assert.False(gone.Audible);
		}

		[Fact]
		public void Volume_RangesOnTheSharedCurve() {
			// The edge within melee reach plays at the zone's full loudness, like the
			// locator's full-at-swing-reach anchor...
			ZoneCue inReach = ZoneSonifier.Compose(4f, 0f, 2f, 0f, ZonePhase.Active);
			Assert.Equal(ZoneSonifier.MaxVolume, inReach.Params.Volume, 3);

			// ...and the fade reaches true silence exactly where the curve floors: a far
			// hazard is not a live threat, so unlike a beacon there is no floor.
			ZoneCue atFloor = ZoneSonifier.Compose(2f + RangingCurve.FloorRange, 0f, 2f, 0f, ZonePhase.Active);
			Assert.False(atFloor.Audible);
			ZoneCue justInside = ZoneSonifier.Compose(2f + RangingCurve.FloorRange - 0.5f, 0f, 2f, 0f, ZonePhase.Active);
			Assert.True(justInside.Audible);
			Assert.True(justInside.Params.Volume < 0.1f);
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
		public void InsideWhileArming_KeepsTheThrob_AtFullVolume() {
			// Standing in an arming zone keeps the arming throb, just at full volume: the
			// rattle is reserved for a LIVE hazard, so the cycle stays audible mid-crossing
			// and the flip to the rattle is the arming edge itself.
			ZoneCue cue = ZoneSonifier.Compose(0.5f, 0f, 3f, 0f, ZonePhase.Arming);
			Assert.True(cue.Inside);
			Assert.Equal(ZoneSynth.ArmingKey, cue.ClipKey);
			Assert.Equal(1f, cue.Params.Volume, 3);
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

		// ComposePoint is the trap path: the adapter hands over the offset to the nearest
		// dangerous point on the trap's collider instead of authored radii.

		[Fact]
		public void Point_OutsideEast_SoundSitsTowardIt_AtTheGap() {
			ZoneCue cue = ZoneSonifier.ComposePoint(right: 4f, forward: 0f, inside: false, ZonePhase.Active);
			Assert.True(cue.Audible);
			Assert.False(cue.Inside);
			Assert.Equal(ZoneSynth.ActiveKey, cue.ClipKey);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(4f, cue.Distance, 3);
		}

		[Fact]
		public void Point_SilencesAtFalloff() {
			ZoneCue cue = ZoneSonifier.ComposePoint(ZoneSonifier.FalloffRange, 0f, inside: false, ZonePhase.Active);
			Assert.False(cue.Audible);
		}

		[Fact]
		public void Point_PrimedTrap_UsesThePrimedLoop() {
			ZoneCue cue = ZoneSonifier.ComposePoint(4f, 0f, inside: false, ZonePhase.Primed);
			Assert.Equal(ZoneSynth.PrimedKey, cue.ClipKey);
		}

		[Fact]
		public void Point_SafeTrapBeat_UsesTheArmingLoop() {
			// A cycling trap's safe beat is voiced as arming: it exists, damage is off,
			// and leaving (or crossing) is free.
			ZoneCue cue = ZoneSonifier.ComposePoint(4f, 0f, inside: false, ZonePhase.Arming);
			Assert.Equal(ZoneSynth.ArmingKey, cue.ClipKey);
		}

		[Fact]
		public void Point_InsideLiveTrap_RattlesAtFullVolume() {
			// Inside, the adapter passes the bearing toward the trap's center (east here);
			// with the trap live the rattle plays at full volume.
			ZoneCue cue = ZoneSonifier.ComposePoint(2f, 0f, inside: true, ZonePhase.Active);
			Assert.True(cue.Inside);
			Assert.Equal(ZoneSynth.InsideKey, cue.ClipKey);
			Assert.Equal(1f, cue.Params.Volume, 3);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(0f, cue.Distance, 3);
		}

		[Fact]
		public void Point_InsideOnTheSafeBeat_KeepsThePhaseLoop_AtFullVolume() {
			// Crossing a spike floor on its safe beat: the throb (or primed pulse) keeps
			// playing underfoot at full volume, so the crossing is timeable from inside and
			// the flip to the rattle is the moment the trap goes hot. (The first build
			// rattled here, and a floor trap spanning the corridor was uncrossable by ear.)
			ZoneCue arming = ZoneSonifier.ComposePoint(2f, 0f, inside: true, ZonePhase.Arming);
			Assert.True(arming.Inside);
			Assert.Equal(ZoneSynth.ArmingKey, arming.ClipKey);
			Assert.Equal(1f, arming.Params.Volume, 3);
			Assert.Equal(0f, arming.Distance, 3);

			ZoneCue primed = ZoneSonifier.ComposePoint(2f, 0f, inside: true, ZonePhase.Primed);
			Assert.Equal(ZoneSynth.PrimedKey, primed.ClipKey);
			Assert.Equal(1f, primed.Params.Volume, 3);
		}

		// ComposeSegment is the beam path: a damaging line between two endpoints with the
		// beam collider's half-width, the voice at the nearest point on the segment.

		[Fact]
		public void Segment_BesideTheMiddle_SoundSitsPerpendicular_AtTheGap() {
			// A beam running south-to-north 4 east of the player: the nearest point is due
			// east at its middle, so the voice pans hard right at gap 4 minus the width.
			ZoneCue cue = ZoneSonifier.ComposeSegment(
				rightA: 4f, forwardA: -5f, rightB: 4f, forwardB: 5f, radius: 0.5f, ZonePhase.Active);
			Assert.True(cue.Audible);
			Assert.False(cue.Inside);
			Assert.Equal(ZoneSynth.ActiveKey, cue.ClipKey);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(3.5f, cue.Distance, 3);
		}

		[Fact]
		public void Segment_BeyondTheEndpoint_SoundSitsAtTheEndpoint() {
			// The beam spans 3 to 8 east; the player is west of its near end, so the nearest
			// point clamps to that endpoint and the gap is measured to it, not the infinite line.
			ZoneCue cue = ZoneSonifier.ComposeSegment(3f, 0f, 8f, 0f, 0.5f, ZonePhase.Active);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(2.5f, cue.Distance, 3);
		}

		[Fact]
		public void Segment_WithinTheWidth_RattlesAtFullVolume_TowardTheAxis() {
			// The beam runs north-south 0.3 east of the player, width 0.5: inside. The
			// bearing points at the axis (east), so fleeing the sound steps west off the
			// line, the shortest exit.
			ZoneCue cue = ZoneSonifier.ComposeSegment(0.3f, -5f, 0.3f, 5f, 0.5f, ZonePhase.Active);
			Assert.True(cue.Inside);
			Assert.Equal(ZoneSynth.InsideKey, cue.ClipKey);
			Assert.Equal(1f, cue.Params.Volume, 3);
			Assert.True(cue.Params.Pan > 0.9f);
			Assert.Equal(0f, cue.Distance, 3);
		}

		[Fact]
		public void Segment_OnTheAxis_NoBearing_CenteredAtMidPitch() {
			// Standing exactly on the beam's line: no bearing, centered at mid pitch like a
			// zone's center - either side out works.
			ZoneCue cue = ZoneSonifier.ComposeSegment(0f, -5f, 0f, 5f, 0.5f, ZonePhase.Active);
			Assert.True(cue.Inside);
			Assert.Equal(0f, cue.Params.Pan, 3);
			Assert.Equal(ProjectileSonifier.PitchFor(0f), cue.Params.Pitch, 3);
		}

		[Fact]
		public void Segment_GrowingBeam_UsesTheArmingLoop_InsideAndOut() {
			// A beam still growing in voices as arming (leaving its line is free until the
			// grow completes); on the line the same throb plays at full volume, and the
			// flip to the rattle is the grow completing over your head.
			ZoneCue outside = ZoneSonifier.ComposeSegment(4f, -5f, 4f, 5f, 0.5f, ZonePhase.Arming);
			Assert.Equal(ZoneSynth.ArmingKey, outside.ClipKey);

			ZoneCue inside = ZoneSonifier.ComposeSegment(0.2f, -5f, 0.2f, 5f, 0.5f, ZonePhase.Arming);
			Assert.Equal(ZoneSynth.ArmingKey, inside.ClipKey);
			Assert.True(inside.Inside);
			Assert.Equal(1f, inside.Params.Volume, 3);
		}

		// --- Voice selection: the limited voices go to the nearest hazards, with a hold
		// margin so a voiced hazard does not lose its loop to a stranger a half-step closer. ---

		[Fact]
		public void SelectionRank_UnheldIsPlainDistance() {
			Assert.Equal(5f, ZoneSonifier.SelectionRank(5f, held: false), 3);
		}

		[Fact]
		public void SelectionRank_HeldVoice_OutranksASlightlyNearerStranger() {
			float held = ZoneSonifier.SelectionRank(5f, held: true);
			float stranger = ZoneSonifier.SelectionRank(4.5f, held: false);
			Assert.True(held < stranger);
		}

		[Fact]
		public void SelectionRank_ClearlyNearerStranger_TakesTheVoice() {
			float held = ZoneSonifier.SelectionRank(5f, held: true);
			float stranger = ZoneSonifier.SelectionRank(5f * (1f - ZoneSonifier.HoldMargin) - 0.5f, held: false);
			Assert.True(stranger < held);
		}

		[Fact]
		public void SelectionRank_InsideHazard_SeizesAVoiceInstantly() {
			// Distance zero outranks any held voice: an overlapping danger must speak now.
			Assert.True(ZoneSonifier.SelectionRank(0f, held: false)
				< ZoneSonifier.SelectionRank(0.5f, held: true));
		}

		[Fact]
		public void Segment_SilencesAtFalloff() {
			float far = ZoneSonifier.FalloffRange + 0.5f;
			ZoneCue cue = ZoneSonifier.ComposeSegment(far, -5f, far, 5f, 0.5f, ZonePhase.Active);
			Assert.False(cue.Audible);
		}

		[Fact]
		public void Segment_Degenerate_ComposesAsAPoint() {
			// A zero-length segment (engaged at its own origin) is just a point hazard.
			ZoneCue segment = ZoneSonifier.ComposeSegment(4f, 0f, 4f, 0f, 0.5f, ZonePhase.Active);
			Assert.True(segment.Audible);
			Assert.Equal(3.5f, segment.Distance, 3);
			Assert.True(segment.Params.Pan > 0.9f);
		}
	}
}
