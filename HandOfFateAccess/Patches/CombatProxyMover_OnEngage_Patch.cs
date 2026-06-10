using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Feeds a launching mover hazard into the combat audio. Lobs (arcing thrown bombs) and
	/// lightning heads (crawling, target-tracking casts) are damage proxies that fly like
	/// projectiles but never enter CombatManager's projectile list, so the projectile sonifier
	/// cannot find them itself; this registers them for the flight voice and requests the
	/// launch dodge cue at the spawn point, gated per attacker so an attack that already
	/// telegraphed through its own hook (the Hermit's OnThrow, a boss's Begin) does not crack
	/// twice. Hostile movers only, decided once here for both records: the player's own
	/// effects are not threats, and a mover's source never changes after engage.
	///
	/// One postfix shared by each class's own OnEngage override (both verified to declare it;
	/// patching the inherited base would catch every proxy type), which the base Engage calls
	/// after assigning Effect, so the source is readable. The lightning head's damaging trail
	/// segments are NOT voiced here; they are zone hazards for the danger-zone work. Records
	/// only; the sonifier and AttackCues pumps play.
	/// </summary>
	internal static class CombatProxyMover_OnEngage_Patch {
		private static void Postfix(CombatProxy __instance) {
			try {
				Targetable source = __instance.Effect.Source;
				if (!Hostility.ThreatensPlayer(source)) return;
				ProjectileSonification.RecordMover(__instance);
				AttackCues.RecordMoverLaunch(__instance.transform.position, source.GetInstanceID());
			} catch (Exception ex) {
				Log.Error("mover engage hook failed: " + ex);
			}
		}
	}
}
