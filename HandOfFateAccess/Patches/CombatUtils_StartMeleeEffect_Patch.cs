using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the block-or-dodge action cue when a melee attack's parry window opens.
	/// StartMeleeEffect is called from every melee attack action (generic AI, bosses, special
	/// dashes) at that moment, and its a_blockable argument arrives already resolved: each call
	/// site passes CombatUtils.IsBlockable's result (or an equivalent inline check), which folds
	/// in the AIController's ForceUnblockable override. The prefix runs ahead of the method's own
	/// counter-ability gate (the game shows NO flash at all for a blockable attack the player
	/// cannot counter), so a cue fires for every attack: a blind player needs the telegraph
	/// regardless. Which cue is classified by the live Combat.Counter ability, read here at the
	/// same moment the game reads it: with no counter equipped the attack is unblockable for this
	/// player (ActionCounter.Begin skips the counter scan entirely at Counter == 0), so it takes
	/// the dodge cue. a_model is the attacker, so its transform positions the cue. Records only;
	/// the AttackCues pump plays it.
	///
	/// Caught and logged rather than left to crash: an exception escaping a prefix skips the
	/// original method, which here would silently keep the parry window from opening and break
	/// the player's counter for that swing. A logged failure is the visible one.
	/// </summary>
	internal static class CombatUtils_StartMeleeEffect_Patch {
		private static void Prefix(Model a_model, bool a_blockable) {
			try {
				bool canBlock = PlayerController.Instance.Combat.Counter > 0;
				AttackCues.RecordAction(a_blockable, canBlock, a_model.transform.position,
					AttackCues.SourceKeyFrom(a_model));
			} catch (Exception ex) {
				Log.Error("melee action cue failed: " + ex);
			}
		}
	}
}
