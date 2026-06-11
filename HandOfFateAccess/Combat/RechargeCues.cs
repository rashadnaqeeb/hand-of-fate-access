using System;
using System.Collections.Generic;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The ability-recharge cue: a chime the frame the equipped weapon's or artifact's
	/// ability becomes usable again in a live fight, panned hard to the slot's side
	/// (weapon left, artifact right, the bumper layout). The game's only readout for
	/// this is the radial cooldown fill on the ability's HUD card; this is its audible
	/// equivalent, so the player knows the button works again without test-pressing it.
	///
	/// A cleared arena counts as a hold, like the Dealer quick-time: with no living
	/// enemy there is nothing to spend the ability on, so a recharge that completes
	/// while the player walks to the exit or a boss's end circle stays quiet. If the
	/// level instead spawns its next wave, the deferred chime lands with it, exactly
	/// when the ability matters again. Living means not dead, regardless of hidden or
	/// invulnerable (a submerged Kraken or the shielded Dealer is still a fight).
	///
	/// Usable is the game's own pre-fire gate, re-read live every frame, never cached:
	/// the ability's public <c>CanUse</c> (cooldown, charges, gold and health costs)
	/// plus the controller's disable counters, the same pair of checks the game's
	/// <c>ConditionAbility</c> runs before letting the button fire, so the chime and the
	/// button can never disagree. All public API; no hooks, no reflection. Core's
	/// <see cref="RechargeCueTracker"/> owns the what-plays-when decision (baseline at
	/// fight start, deferral across the Dealer quick-time, reset out of combat); this
	/// adapter only extracts the bools and plays what it decides.
	/// </summary>
	internal sealed class RechargeCues {
		private bool _ready;
		private readonly RechargeCueTracker _tracker = new RechargeCueTracker();

		/// <summary>Load the recharge sample from <paramref name="pluginDir"/>/sounds.
		/// A failed load leaves the feature silent (logged).</summary>
		public void Initialize(string pluginDir) {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; recharge cues disabled");
				return;
			}
			string path = Path.Combine(Path.Combine(pluginDir, "sounds"), RechargeCueComposer.Key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(RechargeCueComposer.Key, pcm, channels, sampleRate);
				_ready = true;
				Log.Debug("recharge cues ready");
			} catch (Exception ex) {
				Log.Error("recharge sample failed to load from " + path + ": " + ex);
			}
		}

		public void Pump() {
			if (!_ready) return;
			bool live = CombatGate.IsLive;
			bool weaponUsable = false;
			bool artifactUsable = false;
			if (live) {
				PlayerController player = PlayerController.Instance;
				Weapon weapon = player.Inventory.Weapon;
				weaponUsable = Usable(weapon != null ? weapon.Ability : null, player.DisableWeaponAbility);
				Artifact artifact = player.Inventory.Artifact;
				artifactUsable = Usable(artifact != null ? artifact.Ability : null, player.DisableArtifactAbility);
			}
			_tracker.Pump(live, DealerQte.IsActive || !AnyLivingEnemy(), weaponUsable, artifactUsable,
				out bool cueWeapon, out bool cueArtifact);
			if (cueWeapon) AudioEngine.PlayOneShot(RechargeCueComposer.Key, RechargeCueComposer.Weapon);
			if (cueArtifact) AudioEngine.PlayOneShot(RechargeCueComposer.Key, RechargeCueComposer.Artifact);
		}

		// The game's pre-fire gate, mirrored from ConditionAbility: a slot with no
		// ability never cues (bare fists, an artifact-less loadout, equipment without
		// an ability), a disabling curse blocks it, and CanUse folds the cooldown, the
		// charge count, and the gold and health costs.
		private static bool Usable(Ability ability, int disableCounter) =>
			ability != null && disableCounter == 0 && ability.CanUse;

		// Whether anything is left to fight. The same live target list the enemy
		// locator answers from, but without its hidden/invulnerable skips: those mean
		// "not attackable this instant", while this asks "is the fight still on". A
		// Targetable without a Destroyable is a state the game's own iterations over
		// this list guard against, so that null check is legitimate.
		private static bool AnyLivingEnemy() {
			CombatManager manager = CombatManager.Instance;
			if (manager == null) return false;
			List<Targetable> targets = manager.Targets;
			for (int i = 0; i < targets.Count; i++) {
				Targetable target = targets[i];
				if (target == null || target.Team != TeamType.Enemy) continue;
				if (target.Destroyable == null || target.Destroyable.IsDead) continue;
				return true;
			}
			return false;
		}
	}
}
