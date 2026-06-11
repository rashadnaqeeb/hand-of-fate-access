using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the dodge cue as the Dealer missile's dodge window opens. This method is
	/// where the game itself reveals its Dodge button prompt (called from the dodge
	/// coroutine exactly as the input window starts), so the hook IS the prompt: crack
	/// means press dodge now, the same answer as an unblockable swing. Hooked here rather
	/// than at the public OnDodge, which early-returns without opening a window when a
	/// reflect is already resolving. The cue sits at the Dealer, where the missile comes
	/// from.
	/// </summary>
	internal static class ActionDealerMissileReaction_ShowDodgeIndicator_Patch {
		private static void Postfix() {
			try {
				UnityEngine.Transform dealer = DealerController.Instance.transform;
				AttackCues.RecordAction(blockable: false, canBlock: false, dealer.position,
					AttackCues.SourceKeyFrom(dealer));
			} catch (Exception ex) {
				Log.Error("dealer missile dodge cue failed: " + ex);
			}
		}
	}
}
