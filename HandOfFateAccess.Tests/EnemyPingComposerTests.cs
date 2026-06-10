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
		public void Volume_RidesTheAlertFloorNotTheAmbientOne() {
			// A far enemy must still answer loud: the ping is a one-shot whose absence
			// means "no enemies", so it takes the telegraph alert floor, well above the
			// projectile loop's faint one.
			var far = EnemyPingComposer.Compose(AttackCueComposer.FalloffRange * 2f, 0f);
			Assert.Equal(AttackCueComposer.MinVolume, far.Volume);
			Assert.True(far.Volume > ProjectileSonifier.MinVolume * 2f);
		}

		[Fact]
		public void Volume_PeaksOnTopOfThePlayer() {
			var onTop = EnemyPingComposer.Compose(0f, 0f);
			Assert.Equal(AttackCueComposer.MaxVolume, onTop.Volume);
		}

		[Fact]
		public void Volume_FallsWithDistanceAtFixedBearing() {
			var near = EnemyPingComposer.Compose(2f, 2f);
			var mid = EnemyPingComposer.Compose(6f, 6f);
			Assert.True(near.Volume > mid.Volume);
			// Bearing is distance-independent: same direction, same pan and pitch.
			Assert.Equal(near.Pan, mid.Pan, 5);
			Assert.Equal(near.Pitch, mid.Pitch, 5);
		}
	}
}
