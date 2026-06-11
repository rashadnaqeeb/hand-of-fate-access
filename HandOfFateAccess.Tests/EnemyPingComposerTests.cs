using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class EnemyPingComposerTests {
		[Fact]
		public void Bearing_MatchesTheProjectileGrammar() {
			// Pan and pitch must be identical to the projectile voices' for the same
			// offset, so the learned spatial language transfers without retraining.
			var ping = EnemyPingComposer.Compose(-3f, 5f);
			var projectile = ProjectileSonifier.Compose(-3f, 5f);
			Assert.Equal(projectile.Pan, ping.Pan);
			Assert.Equal(projectile.Pitch, ping.Pitch);
		}

		[Fact]
		public void Volume_FullThroughoutMeleeReach() {
			// Full volume means "in swing reach": anywhere inside the game's default
			// melee attack range pings at peak, not just on top of the player.
			Assert.Equal(EnemyPingComposer.MaxVolume, EnemyPingComposer.VolumeFor(0f));
			Assert.Equal(EnemyPingComposer.MaxVolume, EnemyPingComposer.VolumeFor(EnemyPingComposer.MeleeRange));
		}

		[Fact]
		public void Volume_HoldsTheFloorAcrossTheArena() {
			// Far enemies are quiet but never silent: a silent ping reads as "no enemies".
			Assert.Equal(EnemyPingComposer.MinVolume, EnemyPingComposer.VolumeFor(EnemyPingComposer.FloorRange));
			Assert.Equal(EnemyPingComposer.MinVolume, EnemyPingComposer.VolumeFor(EnemyPingComposer.FloorRange * 3f));
			Assert.True(EnemyPingComposer.MinVolume > 0f);
		}

		[Fact]
		public void Volume_DegenerateDistanceDropsToTheFloor() {
			Assert.Equal(EnemyPingComposer.MinVolume, EnemyPingComposer.VolumeFor(float.NaN));
			Assert.Equal(EnemyPingComposer.MinVolume, EnemyPingComposer.VolumeFor(float.PositiveInfinity));
		}

		[Fact]
		public void Volume_FallsWithDistanceAtFixedBearing() {
			var near = EnemyPingComposer.Compose(3f, 3f);
			var mid = EnemyPingComposer.Compose(8f, 8f);
			Assert.True(near.Volume > mid.Volume);
			// Bearing is distance-independent: same direction, same pan and pitch.
			Assert.Equal(near.Pan, mid.Pan, 5);
			Assert.Equal(near.Pitch, mid.Pitch, 5);
		}

		[Fact]
		public void Volume_MidpointSitsMidCurve() {
			// Linear between melee reach and the floor: halfway out is halfway down.
			float mid = (EnemyPingComposer.MeleeRange + EnemyPingComposer.FloorRange) * 0.5f;
			float expected = (EnemyPingComposer.MaxVolume + EnemyPingComposer.MinVolume) * 0.5f;
			Assert.Equal(expected, EnemyPingComposer.VolumeFor(mid), 5);
		}
	}
}
