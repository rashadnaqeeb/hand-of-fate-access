using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the dodge cue as the Hermit releases a bomb. Unlike the other bespoke boss
	/// attacks this one is not cued at Begin: the action runs a retreat-and-hold machine for
	/// seconds before the throw, so Begin is misleadingly early, while OnThrow is the moment
	/// the bomb actually leaves the hand. The cue sits at the thrower, naming the direction
	/// the bomb comes from; voicing the bomb's flight and landing zone belongs to the proxy
	/// work, not this hook.
	/// </summary>
	internal static class ActionHermitBomb_OnThrow_Patch {
		private static void Prefix(ActionHermitBomb __instance) {
			try {
				AttackCues.RecordAction(false, __instance.ActorTransform.position);
			} catch (Exception ex) {
				Log.Error("hermit bomb cue failed: " + ex);
			}
		}
	}
}
