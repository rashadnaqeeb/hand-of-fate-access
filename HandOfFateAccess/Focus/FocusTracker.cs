using UnityEngine;

namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Shared focus state between the SetSelection hook (producer) and the Update
	/// loop (consumer). The hook only records; speaking happens once per frame in
	/// Update, per the announce-from-update-loop rule. Consecutive selections of
	/// the same object are ignored so a re-select doesn't re-announce.
	/// </summary>
	internal static class FocusTracker {
		private static GameObject _pending;
		private static GameObject _lastRecorded;
		private static bool _pendingUserInitiated;
		private static bool _dirty;

		/// <summary>
		/// Whether a focus change is recorded but not yet consumed. Read by the screen
		/// watcher to decide if a screen announcement actually has a following focus to
		/// queue after it.
		/// </summary>
		public static bool HasPending => _dirty;

		/// <summary>Called from the hook when controller/keyboard focus changes.</summary>
		public static void Record(GameObject go) {
			if (go == _lastRecorded) return;
			_lastRecorded = go;
			_pending = go;
			// Stamp origin at record time, not consume time: this runs inside the input
			// dispatch (same frame) for user-driven changes, whereas TryConsume may run a
			// later frame when the frame stamp no longer matches.
			_pendingUserInitiated = NavigationState.IsActive;
			_dirty = true;
		}

		/// <summary>
		/// Forces the current selection to be re-announced even though it has not changed, for
		/// when its readout changed because the context did, not the selection: the chance
		/// gambit entering its pick phase, where the already-selected card now reads as a slot
		/// number rather than "face down card". Without this the focus path, which only fires on
		/// a selection change, would not speak the new readout until the player first moved.
		/// No-op if nothing is selected.
		/// </summary>
		public static void Refresh(bool userInitiated) {
			GameObject go = UICamera.selectedObject;
			if (go == null) return;
			_pending = go;
			_lastRecorded = go;
			_pendingUserInitiated = userInitiated;
			_dirty = true;
		}

		/// <summary>
		/// If focus changed since the last call, hand back the focused object and
		/// whether the user drove the change, and clear the dirty flag. Returns false
		/// when nothing is pending.
		/// </summary>
		public static bool TryConsume(out GameObject go, out bool userInitiated) {
			if (!_dirty) {
				go = null;
				userInitiated = false;
				return false;
			}
			_dirty = false;
			go = _pending;
			userInitiated = _pendingUserInitiated;
			return true;
		}
	}
}
