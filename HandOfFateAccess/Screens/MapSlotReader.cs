using System.Collections.Generic;
using HandOfFateAccess.Focus;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads a focused map slot off the live model. A board slot is a CardContainer: its
	/// TopCard is the encounter the player acts on, and a spice event can deal
	/// supply/equipment/pain cards underneath it (dealt at the bottom of the stack), which
	/// the game offsets so they peek out on the board. The generic focus path would sweep
	/// every UILabel under the slot and read all the stacked cards as one jumble; this
	/// reads the stack structurally instead - the top card as a card, the rest as attached
	/// items. Values are read live; nothing is cached.
	/// </summary>
	internal static class MapSlotReader {
		/// <summary>The focused slot's readout, or null when the slot is empty (no card to
		/// read, so the caller falls back to generic handling).</summary>
		public static MapSlotInfo Read(MapLayoutSlot slot) {
			Card top = slot.TopCard;
			if (top == null) return null;

			// Everything stacked under the top card is an attached spice (a non-encounter
			// supply/equipment/pain card the event laid on this encounter). Stack order
			// matches the list order, top card last.
			var spices = new List<CardFace>();
			IList<Card> cards = slot.Cards;
			for (int i = 0; i < cards.Count; i++) {
				Card c = cards[i];
				if (c != top)
					spices.Add(ReadFace(c));
			}

			return new MapSlotInfo(ReadFace(top), spices);
		}

		// The encounter a sighted player sees on the board is face-up; only hidden
		// quest/special cards are flipped. Read the card when face-up, withhold its
		// identity when flipped (the same rule the zoom uses), for the top card and any
		// stacked card alike so a hidden card is never spoken in either position.
		private static CardFace ReadFace(Card card) {
			return card.Flipped ? new CardFace(true, null) : new CardFace(false, ProxyFactory.ExtractCard(card));
		}
	}
}
