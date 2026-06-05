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
			_dirty = true;
		}

		/// <summary>
		/// If focus changed since the last call, hand back the focused object and
		/// clear the dirty flag. Returns false when nothing is pending.
		/// </summary>
		public static bool TryConsume(out GameObject go) {
			if (!_dirty) {
				go = null;
				return false;
			}
			_dirty = false;
			go = _pending;
			return true;
		}
	}
}
