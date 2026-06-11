using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The on-demand enemy locator: pressing the locator key in a fight answers with one
	/// synthesized ping at the nearest living enemy, composed by Core's
	/// <see cref="EnemyPingComposer"/> (the projectile voices' bearing grammar, with
	/// loudness as a melee-reach-to-arena distance readout), so the skill of reading a
	/// projectile's bearing transfers directly to reading the enemy's. One enemy, one ping: the nearest is the
	/// one to fight or flee, and a press per ping keeps the answer in the player's
	/// control. Silence in a fight means nothing attackable: no living enemy, or none
	/// that damage can currently touch.
	/// </summary>
	internal sealed class EnemyLocator {
		private const int RenderSampleRate = 44100;

		private bool _ready;

		/// <summary>Render and register the ping clip. Skipped (logged) when the audio
		/// backend never came up, so the trigger degrades to a no-op.</summary>
		public void Initialize() {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; enemy locator disabled");
				return;
			}
			AudioEngine.Register(EnemyPingSynth.Key, EnemyPingSynth.Render(RenderSampleRate), 1, RenderSampleRate);
			_ready = true;
			Log.Debug("enemy locator ready");
		}

		/// <summary>
		/// Ping the nearest living enemy, from the locator key. Outside a live fight the
		/// press is inert: the combat frame carries the lifecycle gate, so a press during
		/// pause, the intro, or the post-combat resolution plays nothing.
		/// </summary>
		public void Trigger() {
			if (!_ready) return;
			// Inert during the Dealer's missile quick-time too: the player is teleported and
			// the camera overridden, so a bearing projected there would mislead.
			if (CombatManager.Instance == null || DealerQte.IsActive
					|| !CombatFrame.TryGet(out CombatFrame frame)) return;

			// An invulnerable enemy is no answer either: damage cannot touch it (the Dealer
			// outside his vulnerability windows, a boss mid-dash or mid-teleport, an escaping
			// goblin), so the ping names what can actually be fought and silence means nothing
			// attackable right now. Combo challenges are the exception: there the game sets
			// every spawned enemy invulnerable as the scoring rule (hits build combo, kills
			// never happen), so the skip would mute the locator for the whole encounter.
			bool skipInvulnerable = CombatEncounter.Instance.RequiredCombo == 0;

			// The nearest living hostile. Hidden ones (a submerged Kraken) are skipped:
			// the game's own attacks cannot target them, and a sighted player cannot see
			// them either. A Targetable without a Destroyable is a state the game itself
			// expects (its own iterations over this list guard the same read), so the
			// null check is legitimate, not defensive.
			Targetable nearest = null;
			float nearestSqr = float.MaxValue;
			List<Targetable> targets = CombatManager.Instance.Targets;
			for (int i = 0; i < targets.Count; i++) {
				Targetable target = targets[i];
				if (target == null || target.Team != TeamType.Enemy) continue;
				if (target.Destroyable == null || target.Destroyable.IsDead || target.Hidden) continue;
				if (skipInvulnerable && target.Destroyable.Invulnerable > 0) continue;
				float sqr = (target.transform.position - frame.Origin).sqrMagnitude;
				if (sqr >= nearestSqr) continue;
				nearest = target;
				nearestSqr = sqr;
			}

			if (nearest == null) {
				Log.Debug("enemy locator: no living enemies");
				return;
			}

			frame.Project(nearest.transform.position, out float right, out float forward);
			AudioEngine.PlayOneShot(EnemyPingSynth.Key, EnemyPingComposer.Compose(right, forward));
		}
	}
}
