using HandOfFateAccess.Focus;
using UnityEngine;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on UICamera.SetSelection, the static chokepoint fired on every
	/// selection change. We only record the focused object and a dirty flag here;
	/// the announcement is composed and spoken once per frame from the Update loop.
	/// Scoped to the Controller scheme (NGUI also routes keyboard arrow nav through
	/// it), so mouse hover doesn't announce.
	///
	/// This catches navigation that syncs to UICamera; selections set only on the
	/// game's own UISelection layer (initial/auto focus on screen entry) bypass this
	/// and are caught by UISelectable_Select_Patch instead.
	/// </summary>
	internal static class UICamera_SetSelection_Patch {
		private static void Postfix(GameObject go, UICamera.ControlScheme scheme) {
			if (scheme != UICamera.ControlScheme.Controller) return;
			if (go == null) return;
			FocusTracker.Record(go);
		}
	}
}
