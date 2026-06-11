using System;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class AttackCueComposerTests {
		private static float NorthPitch => AttackCueComposer.PitchFor(1f);
		private static float SouthPitch => AttackCueComposer.PitchFor(-1f);
		private static float MidPitch => AttackCueComposer.PitchFor(0f);

		// --- Sample choice: block only when the attack is blockable AND the player can act
		// on it; everything else is dodge. ---

		[Fact]
		public void Blockable_WithAbility_TakesBlockSample() {
			Assert.Equal(AttackCueComposer.BlockKey, AttackCueComposer.ActionKey(true, canBlock: true));
		}

		[Fact]
		public void Blockable_WithoutAbility_TakesDodgeSample() {
			// The cue is an action instruction. Without the counter/reflect ability the block
			// input does nothing, so a "blockable" attack is unblockable for this player and
			// the only survivable instruction is dodge.
			Assert.Equal(AttackCueComposer.DodgeKey, AttackCueComposer.ActionKey(true, canBlock: false));
		}

		[Fact]
		public void Unblockable_TakesDodgeSample() {
			Assert.Equal(AttackCueComposer.DodgeKey, AttackCueComposer.ActionKey(false, canBlock: true));
		}

		[Fact]
		public void BlockAndDodge_AreDistinctSamples() {
			// Block and dodge demand opposite reactions, so they must never resolve to one sound.
			Assert.NotEqual(AttackCueComposer.ActionKey(true, true), AttackCueComposer.ActionKey(false, true));
		}

		// --- The trap-shot lane test: a spear fires forever down its lane, so only a shot
		// whose path covers where the player stands deserves a cue. ---

		[Fact]
		public void ShotAimedAtThePlayer_Threatens() {
			// Spawned 10 east, flying due west: straight at the origin.
			Assert.True(AttackCueComposer.ShotThreatens(10f, 0f, -1f, 0f));
		}

		[Fact]
		public void ShotGrazingTheLaneEdge_Threatens() {
			// A lane passing just inside the half-width still covers the player's body.
			Assert.True(AttackCueComposer.ShotThreatens(10f, AttackCueComposer.LaneHalfWidth * 0.7f, -1f, 0f));
		}

		[Fact]
		public void ShotDownANeighboringLane_DoesNot() {
			// Parallel lane two half-widths away: it can never hit, so it never cues.
			Assert.False(AttackCueComposer.ShotThreatens(10f, AttackCueComposer.LaneHalfWidth * 2f, -1f, 0f));
		}

		[Fact]
		public void ShotFlyingAway_DoesNot() {
			// Same lane, opposite direction: the closest approach is behind the spawn.
			Assert.False(AttackCueComposer.ShotThreatens(10f, 0f, 1f, 0f));
		}

		[Fact]
		public void DiagonalShot_NormalizesItsDirection() {
			// Spawned northeast flying southwest, direction passed unnormalized: dead on.
			Assert.True(AttackCueComposer.ShotThreatens(5f, 5f, -3f, -3f));
		}

		[Fact]
		public void DegenerateDirection_NeverThreatens() {
			Assert.False(AttackCueComposer.ShotThreatens(10f, 0f, 0f, 0f));
		}

		// --- Bearing: pan is east/west, pitch is down-biased and brightest due north. ---

		[Fact]
		public void DueNorth_Centered_BrightestUnshifted() {
			SoundParams sp = AttackCueComposer.Compose(0f, 5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(1f, sp.Pitch, 3);
		}

		[Fact]
		public void DueSouth_Centered_Darkest() {
			SoundParams sp = AttackCueComposer.Compose(0f, -5f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal((float)Math.Pow(2.0, -AttackCueComposer.DownOctaves), sp.Pitch, 3);
		}

		[Fact]
		public void DueWest_FullLeft_MidPitch() {
			SoundParams sp = AttackCueComposer.Compose(-5f, 0f);
			Assert.Equal(-1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void DueEast_FullRight_MidPitch() {
			SoundParams sp = AttackCueComposer.Compose(5f, 0f);
			Assert.Equal(1f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void PitchRisesNorthward() {
			Assert.True(SouthPitch < MidPitch);
			Assert.True(MidPitch < NorthPitch);
		}

		[Fact]
		public void PitchNeverAboveUnshifted() {
			for (float d = -1f; d <= 1f; d += 0.25f)
				Assert.True(AttackCueComposer.PitchFor(d) <= 1f);
		}

		[Fact]
		public void Northwest_HalfLeft_AboveMid() {
			SoundParams sp = AttackCueComposer.Compose(-3f, 3f);
			Assert.Equal(-0.5f, sp.Pan, 3);
			Assert.True(sp.Pitch > MidPitch && sp.Pitch < 1f);
		}

		// --- Bearing is distance-independent: only direction sets pan and pitch. ---

		[Fact]
		public void SameBearing_SamePanAndPitch_AnyDistance() {
			SoundParams near = AttackCueComposer.Compose(-1f, 1f);
			SoundParams far = AttackCueComposer.Compose(-20f, 20f);
			Assert.Equal(near.Pan, far.Pan, 3);
			Assert.Equal(near.Pitch, far.Pitch, 3);
		}

		[Fact]
		public void OnPlayer_Centered_MidPitch() {
			SoundParams sp = AttackCueComposer.Compose(0f, 0f);
			Assert.Equal(0f, sp.Pan, 3);
			Assert.Equal(MidPitch, sp.Pitch, 3);
		}

		[Fact]
		public void Bearing_StaysWithinSeamClamps() {
			SoundParams sp = AttackCueComposer.Compose(-100f, 3f);
			Assert.InRange(sp.Pan, -1f, 1f);
			Assert.InRange(sp.Pitch, SoundParams.MinPitch, SoundParams.MaxPitch);
		}

		// --- Volume: a telegraph is reaction-critical, so it stays loud and floors high. ---

		[Fact]
		public void OnPlayer_LoudestVolume() {
			Assert.Equal(AttackCueComposer.MaxVolume, AttackCueComposer.VolumeFor(0f), 3);
		}

		[Fact]
		public void AtFalloffRange_FloorVolume() {
			Assert.Equal(AttackCueComposer.MinVolume, AttackCueComposer.VolumeFor(AttackCueComposer.FalloffRange), 3);
		}

		[Fact]
		public void BeyondFalloffRange_HoldsFloor() {
			Assert.Equal(AttackCueComposer.MinVolume, AttackCueComposer.VolumeFor(AttackCueComposer.FalloffRange * 3f), 3);
		}

		[Fact]
		public void Closer_IsLouder() {
			float near = AttackCueComposer.VolumeFor(AttackCueComposer.FalloffRange / 4f);
			float far = AttackCueComposer.VolumeFor(AttackCueComposer.FalloffRange / 2f);
			Assert.True(near > far);
		}

		[Fact]
		public void FarTelegraph_StaysClearlyAudible() {
			// The floor is a high one: an attack across the arena must still be heard, not fade
			// to ambient detail the way a tracked projectile may.
			Assert.True(AttackCueComposer.VolumeFor(AttackCueComposer.FalloffRange * 10f) >= 0.5f);
		}

		[Fact]
		public void DegenerateDistance_DropsToFloor_NotABlast() {
			Assert.Equal(AttackCueComposer.MinVolume, AttackCueComposer.VolumeFor(float.NaN), 3);
		}
	}
}
