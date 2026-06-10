using System.Collections.Generic;
using System.Linq;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The map slot readout: the top encounter read as a card (or as face-down when
	/// hidden), followed by any cards a spice event stacked under it, each framed as an
	/// attached item. A face-down card withholds its identity in either position. This is
	/// what replaced the generic label sweep that read every stacked card as one jumble.
	/// </summary>
	public class MapSlotElementTests {
		private static string Describe(MapSlotInfo info) =>
			new MapSlotElement(info).Describe().Resolve();

		private static CardFace Up(CardInfo card) => new CardFace(false, card);
		private static readonly CardFace Down = new CardFace(true, null);

		private static MapSlotInfo Slot(CardFace top, params CardFace[] spices) =>
			new MapSlotInfo(top, spices.ToList());

		[Fact]
		public void Plain_encounter_reads_as_a_card() {
			var card = new CardInfo("Goblins", "Deal damage", "", "");
			Assert.Equal("Goblins, Deal damage", Describe(Slot(Up(card))));
		}

		[Fact]
		public void Face_down_slot_withholds_identity() {
			Assert.Equal("face down card", Describe(Slot(Down)));
		}

		[Fact]
		public void Encounter_with_token_reads_token() {
			var card = new CardInfo("Lizards", "", "", "", hasToken: true);
			Assert.Equal("Lizards, token available", Describe(Slot(Up(card))));
		}

		[Fact]
		public void Completed_encounter_announces_status() {
			var card = new CardInfo("Ambush", "Bandits attack.", "", "", complete: true);
			Assert.Equal("Ambush, completed, Bandits attack.", Describe(Slot(Up(card))));
		}

		[Fact]
		public void Attached_spice_follows_the_encounter() {
			var card = new CardInfo("Goblins", "", "", "");
			var spice = new CardInfo("Apple", "Restores 5 food.", "", "");
			Assert.Equal("Goblins, with Apple, Restores 5 food.", Describe(Slot(Up(card), Up(spice))));
		}

		[Fact]
		public void Multiple_spices_read_in_stack_order() {
			var card = new CardInfo("Skeletons", "", "", "");
			var supply = new CardInfo("Bread", "Restores 5 food.", "", "");
			var pain = new CardInfo("Festering Wound", "Lose 5 health.", "", "");
			Assert.Equal(
				"Skeletons, with Bread, Restores 5 food., with Festering Wound, Lose 5 health.",
				Describe(Slot(Up(card), Up(supply), Up(pain))));
		}

		[Fact]
		public void Face_down_slot_still_reads_an_attached_spice() {
			// A sighted player sees the spice peeking even when the encounter is hidden.
			var spice = new CardInfo("Apple", "Restores 5 food.", "", "");
			Assert.Equal("face down card, with Apple, Restores 5 food.", Describe(Slot(Down, Up(spice))));
		}

		[Fact]
		public void Face_down_spice_withholds_its_identity() {
			var card = new CardInfo("Goblins", "", "", "");
			Assert.Equal("Goblins, with face down card", Describe(Slot(Up(card), Down)));
		}

		[Fact]
		public void Spice_that_filters_to_nothing_drops_its_connective() {
			var card = new CardInfo("Goblins", "", "", "");
			var empty = new CardInfo("", "", "", "");
			Assert.Equal("Goblins", Describe(Slot(Up(card), Up(empty))));
		}

		[Fact]
		public void Exits_follow_the_cards() {
			var card = new CardInfo("Goblins", "", "", "");
			var exits = new MapExits(up: true, down: false, left: false, right: true);
			Assert.Equal("Goblins, exits up, right",
				new MapSlotElement(Slot(Up(card)), exits).Describe().Resolve());
		}

		[Fact]
		public void Exits_follow_a_face_down_card_and_its_spice() {
			var spice = new CardInfo("Apple", "Restores 5 food.", "", "");
			var exits = new MapExits(up: false, down: true, left: true, right: false);
			Assert.Equal("face down card, with Apple, Restores 5 food., exits down, left",
				new MapSlotElement(Slot(Down, Up(spice)), exits).Describe().Resolve());
		}

		[Fact]
		public void Without_exits_nothing_is_added() {
			var card = new CardInfo("Goblins", "", "", "");
			Assert.Equal("Goblins",
				new MapSlotElement(Slot(Up(card)), new MapExits(false, false, false, false))
					.Describe().Resolve());
		}
	}
}
