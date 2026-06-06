using System.Collections.Generic;

namespace HandOfFateAccess.UI {
	/// <summary>
	/// One card on a map slot as the reader sees it: either face-up with its details, or
	/// face-down with its identity withheld (the card back a sighted player sees). Both
	/// the slot's top card and the cards stacked under it use this, so a hidden card is
	/// never spoken in either position.
	/// </summary>
	public sealed class CardFace {
		/// <summary>The card is face-down; its identity must not be read until revealed.</summary>
		public bool FaceDown { get; }

		/// <summary>The card's details; null when FaceDown.</summary>
		public CardInfo Card { get; }

		public CardFace(bool faceDown, CardInfo card) {
			FaceDown = faceDown;
			Card = card;
		}
	}

	/// <summary>
	/// Plain snapshot of a focused map slot, extracted by the plugin's MapSlotReader.
	/// A board slot is a card stack: the top card is the encounter the player acts on,
	/// and an event can deal supply/equipment/pain cards underneath it ("spicing"),
	/// which sit offset and peek out so a sighted player sees them too. This carries the
	/// top card plus those attached cards so the readout matches the board, instead of
	/// the generic label sweep that would blindly read every card stacked on the slot.
	/// Holds no Unity types; composition lives in MapSlotElement and is unit-tested.
	/// </summary>
	public sealed class MapSlotInfo {
		/// <summary>The top card (the encounter the player acts on).</summary>
		public CardFace Top { get; }

		/// <summary>The cards stacked under the top one (attached supply/equipment/pain
		/// from a spice event), in stack order. Empty when nothing is attached.</summary>
		public IList<CardFace> Spices { get; }

		public MapSlotInfo(CardFace top, IList<CardFace> spices) {
			Top = top;
			Spices = spices ?? new List<CardFace>();
		}
	}
}
