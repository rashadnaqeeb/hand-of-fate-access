using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the block cue as the Dealer missile's counter window opens. This method is
	/// where the game itself reveals its Counter button prompt (called from the reflect
	/// coroutine exactly as the input window starts), so the hook IS the prompt, spoken
	/// in the learned vocabulary: clang means press counter now and it connects, the same
	/// answer as a blockable swing. Hooked here rather than at the public OnReflect so a
	/// path that never shows the prompt never cues. The cue sits at the Dealer, where the
	/// missile comes from.
	/// </summary>
	internal static class ActionDealerMissileReaction_ShowReflectIndicator_Patch {
		private static void Postfix() {
			try {
				UnityEngine.Transform dealer = DealerController.Instance.transform;
				AttackCues.RecordAction(blockable: true, canBlock: true, dealer.position,
					AttackCues.SourceKeyFrom(dealer));
			} catch (Exception ex) {
				Log.Error("dealer missile counter cue failed: " + ex);
			}
		}
	}
}
