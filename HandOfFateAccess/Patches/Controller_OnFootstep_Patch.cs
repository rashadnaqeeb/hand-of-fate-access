using HandOfFateAccess.Combat;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Drops the player's footstep and handstep sounds while the player is blocked
	/// against geometry and not actually moving, and turns each swallowed step into a
	/// collision bump instead. The walk animation keeps firing footstep_* animation events
	/// when the player holds into a wall, so the game plays steps for movement that is not
	/// happening; replacing them with a directional bump tells the player they have walked
	/// into something rather than across open floor. Enemy controllers are left untouched,
	/// since their footsteps are useful spatial information.
	///
	/// OnFootstep(Footstep) is the single chokepoint for both foot and hand steps (it is
	/// called for every Footstep value from the animation-event dispatch), so one prefix
	/// covers them all. Movement state comes from <see cref="PlayerMotion"/>, refreshed
	/// each frame from the update pump.
	/// </summary>
	internal static class Controller_OnFootstep_Patch {
		// Returns false to skip the original (no sound), true to let the step play.
		private static bool Prefix(Controller __instance) {
			if (__instance != PlayerController.Instance) return true;
			if (PlayerMotion.IsMoving) return true;
			// A step fired while the player is not translating: they are walking into a
			// wall. Swallow the footstep and signal the collision cue to bump instead.
			PlayerMotion.NoteBlockedStep();
			return false;
		}
	}
}
