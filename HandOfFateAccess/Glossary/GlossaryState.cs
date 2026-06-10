using System.Collections.Generic;
using UnityEngine;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// The mailbox between the game's input dispatch and the glossary pump. While the
	/// glossary overlay is open, the selection sits on the injected glossary button and
	/// the game's UICamera funnels EVERY device into it: keyboard arrows, WASD, dpad and
	/// stick all arrive as OnKey arrow codes; Escape, Tab-as-cancel, controller B and
	/// Start all arrive as OnKey Escape; Enter, controller A and mouse click as DoClick.
	/// So capturing at the two patched chokepoints covers every input device with one
	/// mechanism, and the glossary reads no raw input itself. Per the hook discipline
	/// the patches only record here; the pump consumes and acts.
	/// </summary>
	internal static class GlossaryState {
		/// <summary>The glossary overlay is open over the pause menu. Set by the pump.</summary>
		public static bool Open;

		private static bool _openRequested;
		private static bool _playRequested;
		private static readonly Queue<KeyCode> _pendingKeys = new Queue<KeyCode>();

		/// <summary>From the injected button's ClickAction: open on the next pump.</summary>
		public static void RequestOpen() {
			_openRequested = true;
		}

		public static bool ConsumeOpenRequest() {
			bool requested = _openRequested;
			_openRequested = false;
			return requested;
		}

		/// <summary>
		/// From the OnKey prefix: capture a key the open glossary owns. True means the
		/// game must skip the key. Up/down navigate and Escape closes, on the pump;
		/// left/right are consumed without action so the pause selection underneath
		/// cannot move. Anything else (Tab, Delete) falls through to the game.
		/// </summary>
		public static bool CaptureKey(KeyCode key) {
			if (!Open) return false;
			switch (key) {
				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
				case KeyCode.Escape:
					_pendingKeys.Enqueue(key);
					return true;
				case KeyCode.LeftArrow:
				case KeyCode.RightArrow:
					return true;
				default:
					return false;
			}
		}

		/// <summary>From the DoClick prefix: confirm while open replays the current
		/// entry. True means the clicked control underneath must not activate.</summary>
		public static bool CapturePlay() {
			if (!Open) return false;
			_playRequested = true;
			return true;
		}

		public static bool TryDequeueKey(out KeyCode key) {
			if (_pendingKeys.Count == 0) {
				key = KeyCode.None;
				return false;
			}
			key = _pendingKeys.Dequeue();
			return true;
		}

		public static bool ConsumePlayRequest() {
			bool requested = _playRequested;
			_playRequested = false;
			return requested;
		}

		/// <summary>Drop anything captured but not yet pumped, on close or when the
		/// pause menu goes away.</summary>
		public static void DropPending() {
			_pendingKeys.Clear();
			_playRequested = false;
			_openRequested = false;
		}
	}
}
