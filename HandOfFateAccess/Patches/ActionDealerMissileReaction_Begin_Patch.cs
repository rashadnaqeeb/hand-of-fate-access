using HandOfFateAccess.Combat;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Records the Dealer's missile quick-time reaction starting on the player, so
	/// <see cref="DealerQte"/> can hold the environmental voices quiet while it runs (the
	/// event teleports the player onto an authored missile platform and overrides the
	/// camera).
	/// Postfix gated on __result so a Begin that bails never mutes anything; the class
	/// declares its own Begin override, so the patch cannot land on the shared base.
	/// </summary>
	internal static class ActionDealerMissileReaction_Begin_Patch {
		private static void Postfix(ActionDealerMissileReaction __instance, bool __result) {
			if (!__result) return;
			DealerQte.Record(__instance);
		}
	}
}
