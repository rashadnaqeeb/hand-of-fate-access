using System;
using System.Reflection;
using HandOfFateAccess.Util;
using HarmonyLib;
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
	///
	/// A ground area also logs its authored tuning (arming delay, grow time, lifetime, ring
	/// and cone shape, mine flag). Those values live in asset data, invisible to the
	/// decompiled source, and they ground-truth the zone voice: an angle below 360 names a
	/// cone the voice currently over-warns as a disc, and the delay distribution sizes how
	/// much warning the arming throb really gives.
	/// </summary>
	internal static class CombatProxy_Engage_Patch {
		private static readonly FieldInfo AreaDelay = AccessTools.Field(typeof(CombatProxyArea), "m_activationDelay");
		private static readonly FieldInfo AreaGrow = AccessTools.Field(typeof(CombatProxyArea), "m_growTime");
		private static readonly FieldInfo AreaTimeOut = AccessTools.Field(typeof(CombatProxyArea), "m_timeOut");
		private static readonly FieldInfo AreaInner = AccessTools.Field(typeof(CombatProxyArea), "m_innerRadius");
		private static readonly FieldInfo AreaAngle = AccessTools.Field(typeof(CombatProxyArea), "m_angle");
		private static readonly FieldInfo AreaMine = AccessTools.Field(typeof(CombatProxyArea), "m_isProximityMine");

		private static void Postfix(CombatProxy __instance) {
			try {
				if (__instance is CombatProxyProjectile) return;
				Targetable source = __instance.Effect != null ? __instance.Effect.Source : null;
				string team = source != null ? source.Team.ToString() : "unknown team";
				Vector3 p = __instance.transform.position;
				Log.Info("proxy engaged: " + __instance.GetType().Name + ", " + team +
					", at (" + p.x.ToString("F0") + ", " + p.z.ToString("F0") + ")" + Params(__instance));
			} catch (Exception ex) {
				Log.Error("proxy engage log failed: " + ex);
			}
		}

		private static string Params(CombatProxy proxy) {
			if (!(proxy is CombatProxyArea)) return string.Empty;
			return ", delay=" + ((float)AreaDelay.GetValue(proxy)).ToString("F1")
				+ " grow=" + ((float)AreaGrow.GetValue(proxy)).ToString("F1")
				+ " timeout=" + ((float)AreaTimeOut.GetValue(proxy)).ToString("F1")
				+ " inner=" + ((float)AreaInner.GetValue(proxy)).ToString("F1")
				+ " angle=" + ((float)AreaAngle.GetValue(proxy)).ToString("F0")
				+ " mine=" + (bool)AreaMine.GetValue(proxy);
		}
	}
}
