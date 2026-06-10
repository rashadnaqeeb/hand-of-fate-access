using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on CombatProxyBeam.Engage, the one call every beam passes through at spawn.
	/// A beam is not a CombatProxy and keeps no registry: it is spawned as a child of a
	/// parent proxy (the mage triangle, radial bursts, the rotating boss beams) and
	/// announces itself only here, so this is the zone voice's discovery point for line
	/// hazards. Hostile beams only, decided once off the parent proxy's source (a beam's
	/// parent never changes after engage); every ICombatProxyBeamParent in the game is a
	/// CombatProxy, so a parent that is not one has no team to read - it is recorded
	/// anyway (over-warning is the safe direction) and the warn line is the recon.
	/// Records only; the zone pump voices.
	/// </summary>
	internal static class CombatProxyBeam_Engage_Patch {
		private static void Postfix(CombatProxyBeam __instance, ICombatProxyBeamParent a_parent) {
			try {
				CombatProxy parent = a_parent as CombatProxy;
				if (parent != null) {
					Targetable source = parent.Effect.Source;
					if (source == null || source.Team != TeamType.Enemy) return;
				} else {
					Log.Warn("beam parent " + a_parent.GetType().Name +
						" is not a CombatProxy; voicing its beams unconditionally");
				}
				ZoneSonification.RecordBeam(__instance);
			} catch (Exception ex) {
				Log.Error("beam engage hook failed: " + ex);
			}
		}
	}
}
