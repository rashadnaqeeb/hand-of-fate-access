using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class RechargeCueTrackerTests {
		private readonly RechargeCueTracker _tracker = new RechargeCueTracker();

		private (bool weapon, bool artifact) Pump(bool live, bool hold, bool weaponUsable, bool artifactUsable) {
			_tracker.Pump(live, hold, weaponUsable, artifactUsable, out bool cueWeapon, out bool cueArtifact);
			return (cueWeapon, cueArtifact);
		}

		[Fact]
		public void FirstLiveFrameBaselinesWithoutCueing() {
			// Both abilities ready as the fight opens: the player never heard them
			// recharge, so the opening frame must stay silent.
			Assert.Equal((false, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: true));
		}

		[Fact]
		public void CuesOnTheBlockedToUsableEdge() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Assert.Equal((true, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false));
		}

		[Fact]
		public void DoesNotRecueWhileStillUsable() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false);
			Assert.Equal((false, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false));
		}

		[Fact]
		public void RecuesAfterUseAndRecharge() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false);
			// Used: the slot goes blocked, then comes back. That is news again.
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Assert.Equal((true, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false));
		}

		[Fact]
		public void SlotsTrackIndependently() {
			Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false);
			// The weapon was ready at baseline and stays silent; only the artifact edges.
			Assert.Equal((false, true), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: true));
		}

		[Fact]
		public void CombatEndResetsToAFreshBaseline() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Pump(live: false, hold: false, weaponUsable: false, artifactUsable: false);
			// The next fight opens with the weapon ready: baseline again, no cue, even
			// though the last fight left the slot marked blocked.
			Assert.Equal((false, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false));
		}

		[Fact]
		public void NotLiveNeverCues() {
			Pump(live: false, hold: false, weaponUsable: false, artifactUsable: false);
			Assert.Equal((false, false), Pump(live: false, hold: false, weaponUsable: true, artifactUsable: true));
		}

		[Fact]
		public void HoldDefersTheCueUntilLifted() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			// The edge lands during the hold: silent now, chimes the frame it lifts.
			Assert.Equal((false, false), Pump(live: true, hold: true, weaponUsable: true, artifactUsable: false));
			Assert.Equal((true, false), Pump(live: true, hold: false, weaponUsable: true, artifactUsable: false));
		}

		[Fact]
		public void DeferredCueDropsIfNoLongerUsable() {
			Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false);
			Pump(live: true, hold: true, weaponUsable: true, artifactUsable: false);
			// Blocked again before the hold lifted: there is nothing to announce.
			Assert.Equal((false, false), Pump(live: true, hold: false, weaponUsable: false, artifactUsable: false));
		}
	}
}
