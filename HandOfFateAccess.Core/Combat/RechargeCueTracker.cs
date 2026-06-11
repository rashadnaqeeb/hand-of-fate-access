namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides when the ability-recharge cue plays: the frame an equipped ability's
	/// usable state goes from blocked to usable during a live fight. Usable is the
	/// game's own full pre-fire gate (cooldown elapsed, charges left, costs affordable,
	/// no disabling curse), fed in as one bool per slot, so a chime can mean "cooldown
	/// done" or just as well "you can now afford it": every edge is the same fact, the
	/// button works again. The sighted equivalent is the radial cooldown fill on the
	/// ability's HUD card completing.
	///
	/// The first live frame of a fight only records a baseline: an ability that starts
	/// the fight ready was never heard recharging, and cueing it would chime at every
	/// fight's opening. While a hold is up (the Dealer's missile quick-time, or an
	/// arena cleared of living enemies) a fresh edge is deferred, not dropped: the
	/// slot stays unannounced, so the chime lands when the hold lifts (the next wave
	/// spawning), unless the ability stopped being usable in between, while a hold
	/// that lasts until the fight resolves ends in the reset, never a chime. Out of
	/// combat everything resets, so a new fight always opens on a fresh baseline.
	///
	/// This is a "what the player hears" decision, so it lives in Core and is
	/// unit-tested; the plugin reads the live game state and feeds the bools in.
	/// </summary>
	public sealed class RechargeCueTracker {
		private bool _primed;
		private bool _weaponAnnounced;
		private bool _artifactAnnounced;

		/// <summary>
		/// Advance one frame. <paramref name="live"/> is the combat gate (false resets);
		/// <paramref name="hold"/> defers any cue while true; <paramref name="weaponUsable"/>
		/// and <paramref name="artifactUsable"/> mean "this slot's ability would fire right
		/// now". The out flags order this frame's cue playback per slot.
		/// </summary>
		public void Pump(bool live, bool hold, bool weaponUsable, bool artifactUsable,
				out bool cueWeapon, out bool cueArtifact) {
			cueWeapon = false;
			cueArtifact = false;
			if (!live) {
				_primed = false;
				return;
			}
			if (!_primed) {
				_primed = true;
				_weaponAnnounced = weaponUsable;
				_artifactAnnounced = artifactUsable;
				return;
			}
			cueWeapon = Advance(weaponUsable, hold, ref _weaponAnnounced);
			cueArtifact = Advance(artifactUsable, hold, ref _artifactAnnounced);
		}

		// One slot's frame. "Announced" tracks the usable state the player last heard
		// about: it falls the frame the ability stops being usable (the next ready is
		// news again) and rises only when the cue actually plays, which is what keeps an
		// edge under a hold pending instead of lost.
		private static bool Advance(bool usable, bool hold, ref bool announced) {
			if (!usable) {
				announced = false;
				return false;
			}
			if (announced || hold) return false;
			announced = true;
			return true;
		}
	}
}
