using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Shared postfix on the segment-chain proxies' own OnEngage overrides (CombatProxyTrail
	/// and CombatProxyLightning each declare one; patching the inherited base would catch
	/// every proxy type): registers the chain so the zone pump voices its nearest live
	/// segment. The chain's segment list fills behind the moving head AFTER engage, so
	/// recording the parent here covers every segment it will ever lay; the lightning's
	/// flying head itself is the mover hook's job, this is the damaging path it leaves.
	/// Hostile chains only, decided once - a chain's source never changes after engage.
	/// Records only; the zone pump voices.
	/// </summary>
	internal static class CombatProxyChain_OnEngage_Patch {
		private static void Postfix(CombatProxy __instance) {
			try {
				Targetable source = __instance.Effect.Source;
				if (source == null || source.Team != TeamType.Enemy) return;
				ZoneSonification.RecordChain(__instance);
			} catch (Exception ex) {
				Log.Error("chain engage hook failed: " + ex);
			}
		}
	}
}
