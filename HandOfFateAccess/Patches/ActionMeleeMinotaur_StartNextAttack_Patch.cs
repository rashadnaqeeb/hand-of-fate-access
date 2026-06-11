using System.Reflection;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;
using HarmonyLib;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Fires the telegraph cue for each follow-up swing of the minotaur-pattern melee combo.
	/// ActionMeleeMinotaur (Ratman Jack, the minotaurs) opens its parry window, and with it the
	/// effect-start cue, once in Begin, then chains up to m_numberOfAttacksTotal swings through
	/// StartNextAttack with no further effect-start call; unlike ActionMeleeRatman, whose every
	/// swing animation carries a "parry" event, its swing animations have none. So only the
	/// first swing was cued and the rest landed silently (seen live: hits 1.5 to 3.6s after the
	/// last cue). The prefix reads the pre-decrement swing counter: at the authored total the
	/// chain is starting its first swing, already cued from Begin; at zero it is ending; anything
	/// between is a follow-up swing, re-cued with the same live classification the parry-open
	/// cue uses (blockable per CombatUtils.IsBlockable, block-or-dodge per the player's live
	/// counter ability). Records only; the AttackCues pump plays.
	///
	/// Caught and logged rather than left to crash: an exception escaping the prefix would skip
	/// StartNextAttack and freeze the combo mid-chain.
	/// </summary>
	internal static class ActionMeleeMinotaur_StartNextAttack_Patch {
		private static readonly FieldInfo Remaining =
			AccessTools.Field(typeof(ActionMeleeMinotaur), "m_numberOfAttacks");
		private static readonly FieldInfo Total =
			AccessTools.Field(typeof(ActionMeleeMinotaur), "m_numberOfAttacksTotal");

		private static void Prefix(ActionMeleeMinotaur __instance) {
			try {
				int remaining = (int)Remaining.GetValue(__instance);
				if (remaining <= 0 || remaining == (int)Total.GetValue(__instance)) return;
				bool blockable = CombatUtils.IsBlockable(__instance);
				bool canBlock = PlayerController.Instance.Combat.Counter > 0;
				AttackCues.RecordAction(blockable, canBlock, __instance.ActorTransform.position,
					AttackCues.SourceKeyFrom(__instance.ActorTransform));
			} catch (System.Exception ex) {
				Log.Error("minotaur combo swing cue failed: " + ex);
			}
		}
	}
}
