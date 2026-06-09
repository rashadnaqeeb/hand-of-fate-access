using System;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Log-only reconnaissance on the non-projectile damage proxies (beams, ground areas,
	/// lightning, lobs, trails, scatter/select/interval). Only CombatProxyProjectile registers
	/// in a game-side list; everything else announces itself solely through the base class's
	/// non-virtual Engage, so a postfix here sees every spawn regardless of which enemy or
	/// asset wired it. Each engagement logs its concrete type, team, and position; the logs
	/// from real fights decide which proxy types get voiced, and with what sound, before any
	/// audio is built for them. Projectiles are excluded: the tumble already voices and counts
	/// them. Postfix so the proxy's Effect is already assigned when read.
	/// </summary>
	internal static class CombatProxy_Engage_Patch {
		private static void Postfix(CombatProxy __instance) {
			try {
				if (__instance is CombatProxyProjectile) return;
				Targetable source = __instance.Effect != null ? __instance.Effect.Source : null;
				string team = source != null ? source.Team.ToString() : "unknown team";
				Vector3 p = __instance.transform.position;
				Log.Info("proxy engaged: " + __instance.GetType().Name + ", " + team +
					", at (" + p.x.ToString("F0") + ", " + p.z.ToString("F0") + ")");
			} catch (Exception ex) {
				Log.Error("proxy engage log failed: " + ex);
			}
		}
	}
}
