using HandOfFateAccess.Focus;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on UISelectable.Select, the unified point at which a control becomes
	/// selected. It is reached both by controller/keyboard navigation (via OnSelect)
	/// and by programmatic or initial selection (via OnSelectInternal from the
	/// UISelection.SelectedObject setter). The programmatic path does NOT touch
	/// UICamera.SetSelection, so without this hook auto-selected controls -- the main
	/// menu's first button at launch, the cabinet's Start button once a card is
	/// examined -- hold real keyboard focus yet land silently.
	///
	/// Only the active (valid) selection is recorded, so a control re-selected in a
	/// background category beneath a modal does not announce. The announcement is
	/// composed and spoken once per frame from the Update loop; multiple selections
	/// within one frame collapse to the last, which is the one the user ends on.
	///
	/// A control that forwards its selection (sendToSelectable, e.g. a card-stack
	/// container redirecting to its top card) is skipped: Select forwards then
	/// returns, so the real target's own Select already recorded it. Because that
	/// nested call completes before this postfix runs, recording the forwarder here
	/// would overwrite the target and announce the wrong object.
	/// </summary>
	internal static class UISelectable_Select_Patch {
		private static void Postfix(UISelectable __instance) {
			if (__instance.sendToSelectable != null) return;
			if (!__instance.IsSelectable || !__instance.IsValid) return;
			FocusTracker.Record(__instance.gameObject);
		}
	}
}
