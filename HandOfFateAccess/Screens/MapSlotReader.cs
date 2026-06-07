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

		// On the dungeon board a card lies face-down by default. MapLayoutSlotLayout applies
		// a 180 flip for a flipped card (the layout obeys card flip) and another for an
		// encounter that is Complete or Revealed, so a card is face-up when exactly one of
		// those holds and face-down when neither or both do. All confirmed against the live
		// board: default cards read face-down, the completed entrance face-up, the layout's
		// base rotation zero with obeyCardFlip on.
		//
		// So Flipped means the opposite here than in the cabinet and zoom: a default board
		// card has Flipped=false and is still face-down, while a card flipped up by a reveal
		// modifier is face-up. The stacked supply/equipment/pain cards are non-encounters
		// dealt Flipped=false, so they resolve face-down and stay withheld, matching the
		// board (you preview them once when dealt, then they lie face-down). When a card is
		// face-up its identity is read, telling ExtractCard to ignore its own Flipped guard
		// since the board judges visibility here.
		private static CardFace ReadFace(Card card) {
			var encounter = card as EncounterCard;
			bool completedOrRevealed = encounter != null && (encounter.Complete || encounter.Revealed);
			bool faceUp = card.Flipped ^ completedOrRevealed;
			return faceUp
				? new CardFace(false, ProxyFactory.ExtractCard(card, ignoreFlippedFaceDown: true))
				: new CardFace(true, null);
		}
	}
}
