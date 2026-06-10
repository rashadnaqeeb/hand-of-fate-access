using System.Collections.Generic;

namespace HandOfFateAccess.Maps {
	/// <summary>
	/// Shared state between the RevealMapLayoutSlots.Show hook (producer) and the Update
	/// pump (consumer) for map cards flipped face-up by a reveal effect. The game locks
	/// selection to a bare confirm button while the camera flies to the flipping cards,
	/// so the focus path never reads what was revealed. The hook records the game's own
	/// live slot list; nothing is copied, the cards are read off the slots at speech
	/// time, per the announce-from-update rule (the hook only records, never speaks).
	/// </summary>
	internal static class MapReveal {
		private static IList<MapLayoutSlot> _pending;

		/// <summary>Called from the hook when a reveal sequence starts.</summary>
		public static void Record(IList<MapLayoutSlot> slots) => _pending = slots;

		/// <summary>Drop the pending reveal (after a failed read).</summary>
		public static void Clear() => _pending = null;

		/// <summary>
		/// Hand back the revealed slots and clear them, or false while none are pending
		/// or the reveal is still running. The game sets each card's Revealed flag one at
		/// a time over several frames (the hook fires at coroutine creation, before any);
		/// consuming earlier would read a card still face-down.
		/// </summary>
		public static bool TryConsume(out IList<MapLayoutSlot> slots) {
			slots = null;
			if (_pending == null) return false;
			foreach (MapLayoutSlot slot in _pending) {
				var card = slot.TopCard as EncounterCard;
				if (card != null && !card.Revealed) return false;
			}
			slots = _pending;
			_pending = null;
			return slots.Count > 0;
		}
	}
}
