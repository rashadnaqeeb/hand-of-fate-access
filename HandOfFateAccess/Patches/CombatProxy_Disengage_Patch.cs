using System;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// The end-of-life half of the proxy reconnaissance log (see
	/// <see cref="CombatProxy_Engage_Patch"/>): pairs each engagement with its disengage so the
	/// logs show every hazard's lifetime, which is what sizes the future voicing (a loop needs
	/// a stop edge; a sub-second proxy only merits a one-shot).
	/// </summary>
	internal static class CombatProxy_Disengage_Patch {
		private static void Postfix(CombatProxy __instance) {
			try {
				if (__instance is CombatProxyProjectile) return;
				Log.Info("proxy disengaged: " + __instance.GetType().Name);
			} catch (Exception ex) {
				Log.Error("proxy disengage log failed: " + ex);
			}
		}
	}
}
