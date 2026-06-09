using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the dodge cue when one of the bespoke boss attacks begins. The court-card bosses
	/// (Ogre's feast, both Reaper attacks, the Lich's sacrifice, the Orc Shaman's delayed
	/// blast, the Kraken's four moves, the Ratman King's mirror dash) apply damage directly
	/// instead of calling the CombatUtils effect-start chokepoint, so the parry-open hook
	/// never sees them; every one is unblockable by design, so the answer is always to move.
	///
	/// One postfix shared by each class's own Begin override (each verified to declare it;
	/// patching an inherited Begin would hit the shared base and cue every action in the
	/// game). Postfix rather than prefix so a Begin that bails (__result false) never cues an
	/// attack that did not start. Begin is the earliest commit moment and a deliberate first
	/// cut at timing: the damage tripwire grades the lead time live, and an attack whose log
	/// shows the cue landing uselessly early moves to a tighter in-animation event.
	///
	/// No using System here: these patches need the game's global-namespace Action type, which
	/// System.Action would otherwise make ambiguous.
	/// </summary>
	internal static class BossAction_Begin_Patch {
		private static void Postfix(Action __instance, bool __result) {
			if (!__result) return;
			try {
				AttackCues.RecordAction(false, __instance.ActorTransform.position,
					AttackCues.SourceKeyFrom(__instance.ActorTransform));
			} catch (System.Exception ex) {
				Log.Error("boss attack cue failed: " + ex);
			}
		}
	}
}
