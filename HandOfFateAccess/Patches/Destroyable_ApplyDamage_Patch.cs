using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Coverage tripwire: logs every damage application to the player together with the attack
	/// it came from, so any damage source the combat cues do not telegraph names itself in the
	/// log the first time it lands instead of staying silently undiscovered. Every damage path
	/// in the game (attacks, proxies, traps, ticking effects) funnels through
	/// Destroyable.ApplyDamage, and the damage carries its effect container; walking that chain
	/// with CombatUtils.GetTopContainer names the originating attack action or trap applicant.
	/// The line also notes how long before the hit the last telegraph cue played: a hit with no
	/// recent cue is a coverage gap to go fix. Diagnostic only, no speech and no sound.
	///
	/// Caught and logged rather than left to crash: an exception escaping the prefix would skip
	/// ApplyDamage entirely, silently making the player (and everything else) unkillable.
	/// </summary>
	internal static class Destroyable_ApplyDamage_Patch {
		private static void Prefix(Destroyable __instance, Destroyable.Damage a_damage) {
			try {
				if (__instance.GetComponent<PlayerController>() == null) return;
				if (__instance.IsDead) return;

				ICombatEffectContainer top =
					a_damage.Container == null ? null : CombatUtils.GetTopContainer(a_damage.Container);
				string attack = top == null ? "unknown (no container)" : top.GetType().Name;
				string attacker = top != null && top.Source != null ? top.Source.gameObject.name : "unknown attacker";

				float last = AttackCues.LastCueTime;
				string cue = last < 0f
					? "no cue played yet"
					: "last cue " + (Time.time - last).ToString("F2") + "s before";
				// Invulnerable damage is absorbed by the early return in ApplyDamage, but a
				// source hitting an invulnerable player is still a source worth naming.
				string absorbed = __instance.Invulnerable > 0 ? "; absorbed (invulnerable)" : "";

				Log.Info("player hit: " + a_damage.Amount.ToString("F1") + " from " + attack +
					" (" + attacker + "); " + cue + absorbed);
			} catch (Exception ex) {
				Log.Error("damage tripwire failed: " + ex);
			}
		}
	}
}
