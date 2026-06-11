using System;
using System.Reflection;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the telegraph cue for a trap-fired projectile as it spawns. Action-fired shots
	/// (archers, mages, bosses) cue when their parry window opens via the ranged
	/// effect-start hook, but a trap's spear comes from a scripted applicant that opens no
	/// window, so without this its first sound is the flight voice of a spear already
	/// moving. Only shots whose straight path covers where the player stands cue (Core's
	/// lane test): a spear wall fires its lanes on a timer forever, aimed at no one, and
	/// cueing every volley of every wall would be a wall of noise. Classified like any
	/// ranged attack: the block cue when the projectile can be reflected (its serialized
	/// flag, read live; the player's counter reflects any willing projectile regardless of
	/// source) and the player holds a reflect, else dodge. The per-source volley window in
	/// the pump folds one volley's simultaneous spears into one crack. Records only; the
	/// AttackCues pump plays.
	///
	/// The postfix sits on CombatProxyProjectile's own OnEngage override; the mover classes
	/// (lob, lightning) are separate CombatProxy subclasses with their own hook, so nothing
	/// double-fires.
	/// </summary>
	internal static class CombatProxyProjectile_OnEngage_Patch {
		// The prefab's "a counter can reflect this" switch, the ranged analogue of an
		// attack's blockable flag.
		private static readonly FieldInfo CanReflect = AccessTools.Field(typeof(CombatProxyProjectile), "m_canReflect");

		private static void Postfix(CombatProxyProjectile __instance) {
			try {
				// An Action container is an attack that telegraphs through its own hooks;
				// an applicant container is trap or scripted content, the uncued kind.
				if (!(CombatUtils.GetTopContainer(__instance.Effect) is CombatApplicant)) return;
				Targetable source = __instance.Effect.Source;
				if (!Hostility.ThreatensPlayer(source)) return;
				PlayerController player = PlayerController.Instance;
				if (player == null) return;

				// A projectile travels along its transform's forward; the lane test works on
				// the ground plane.
				Vector3 offset = __instance.transform.position - player.transform.position;
				Vector3 direction = __instance.transform.forward;
				if (!AttackCueComposer.ShotThreatens(offset.x, offset.z, direction.x, direction.z)) return;

				bool blockable = (bool)CanReflect.GetValue(__instance);
				bool canBlock = player.Combat.Reflect > 0;
				AttackCues.RecordTrapShot(blockable, canBlock, __instance.transform.position, source.GetInstanceID());
			} catch (Exception ex) {
				Log.Error("trap shot cue hook failed: " + ex);
			}
		}
	}
}
