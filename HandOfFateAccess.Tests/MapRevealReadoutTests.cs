using System.Collections.Generic;
using System.Linq;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The map reveal readout: each card flipped face-up by a reveal effect reads with
	/// its full identity, led by "revealed" so the boundary between several cards is
	/// unmistakable. The slots arrive face-up; the pump consumes them only after every
	/// Revealed flag is set.
	/// </summary>
	public class MapRevealReadoutTests {
		private static MapSlotInfo Slot(CardInfo card, params CardInfo[] spices) =>
			new MapSlotInfo(
				new CardFace(false, card),
				spices.Select(s => new CardFace(false, s)).ToList());

		[Fact]
		public void Single_card_leads_with_revealed() {
			var card = new CardInfo("The Stairs Down", "Descend to the next floor.", "", "");
			Assert.Equal("revealed The Stairs Down, Descend to the next floor.",
				MapRevealReadout.Compose(new List<MapSlotInfo> { Slot(card) }));
		}

		[Fact]
		public void Each_card_gets_its_own_lead_word() {
			var stairs = new CardInfo("The Stairs Down", "", "", "");
			var goblins = new CardInfo("Goblins", "Deal damage.", "", "");
			Assert.Equal("revealed The Stairs Down, revealed Goblins, Deal damage.",
				MapRevealReadout.Compose(new List<MapSlotInfo> { Slot(stairs), Slot(goblins) }));
		}

		[Fact]
		public void Attached_spice_reads_inside_its_card() {
			var card = new CardInfo("Goblins", "", "", "");
			var spice = new CardInfo("Apple", "Restores 5 food.", "", "");
			Assert.Equal("revealed Goblins, with Apple, Restores 5 food.",
				MapRevealReadout.Compose(new List<MapSlotInfo> { Slot(card, spice) }));
		}

		[Fact]
		public void Empty_or_null_list_reads_nothing() {
			Assert.Equal("", MapRevealReadout.Compose(new List<MapSlotInfo>()));
			Assert.Equal("", MapRevealReadout.Compose(null));
		}
	}
}
