using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the action cue when a ranged attack's parry window opens, the ranged counterpart to
	/// <see cref="CombatUtils_StartMeleeEffect_Patch"/>. For ranged attacks "blockable" means the
	/// shot can be reflected, which the player does with the same block input, so a blockable
	/// ranged attack takes the block cue and an unblockable one the dodge cue, the same mapping as
	/// melee. The ability gate is the ranged one: the cue classifies by the live Combat.Reflect,
	/// matching the game's own gate in StartRangedEffect, so a shot the player has no reflect for
	/// takes the dodge cue (the reflect input would do nothing). The projectile itself is still
	/// voiced separately by the projectile sonifier; this adds the up-front "reflect or dodge"
	/// classification. a_model is the attacker. Records only; the AttackCues pump plays it.
	///
	/// Caught and logged rather than left to crash: an exception escaping a prefix skips the
	/// original method, which here would silently keep the reflect window from opening for that
	/// shot. A logged failure is the visible one.
	/// </summary>
	internal static class CombatUtils_StartRangedEffect_Patch {
		private static void Prefix(Model a_model, bool a_blockable) {
			try {
				bool canBlock = PlayerController.Instance.Combat.Reflect > 0;
				AttackCues.RecordAction(a_blockable, canBlock, a_model.transform.position,
					AttackCues.SourceKeyFrom(a_model));
			} catch (Exception ex) {
				Log.Error("ranged action cue failed: " + ex);
			}
		}
	}
}
