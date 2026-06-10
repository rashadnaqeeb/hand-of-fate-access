using System.Collections.Generic;
using HandOfFateAccess.Maps;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on RevealMapLayoutSlots.Show, the single coroutine every map reveal effect
	/// funnels through (Explorer's Helmet, the rings and diadem, an encounter's reveal
	/// reward). The game locks selection to a bare confirm button while the camera flies
	/// to the flipping cards, so the focus path never reads what was revealed. Show is an
	/// iterator method, so this fires at coroutine creation, before any card flips; it
	/// records the live slot list and the Update pump waits for the flips before
	/// announcing. Records only; speaking is the pump's job.
	/// </summary>
	internal static class RevealMapLayoutSlots_Show_Patch {
		private static void Postfix(List<MapLayoutSlot> a_slots) => MapReveal.Record(a_slots);
	}
}
