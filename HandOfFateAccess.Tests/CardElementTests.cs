using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The targeted card readout: title first, then status, stat, rules/prompt,
	/// value, and whether a token can be won - with empty model fields dropping out.
	/// </summary>
	public class CardElementTests {
		private static string Describe(CardInfo info) =>
			new CardElement(info).Describe().Resolve();

		[Fact]
		public void Equipment_reads_title_stat_and_rules() {
			var info = new CardInfo(
				title: "Iron Sword",
				description: "A reliable blade.",
				statValueString: "Damage: 5",
				valueString: "");
			Assert.Equal("Iron Sword, Damage: 5, A reliable blade.", Describe(info));
		}

		[Fact]
		public void Title_is_spoken_first() {
			var info = new CardInfo("Goblin", "Deal damage", "", "");
			Assert.StartsWith("Goblin", Describe(info));
		}

		[Fact]
		public void Stat_counter_reads_resource_value() {
			var info = new CardInfo("Gold", "", "Gold: 5", "");
			Assert.Equal("Gold, Gold: 5", Describe(info));
		}

		[Fact]
		public void Value_string_follows_description() {
			var info = new CardInfo("Ring of Power", "Boosts attack.", "Damage: 2", "Value: 10 gold");
			Assert.Equal("Ring of Power, Damage: 2, Boosts attack., Value: 10 gold", Describe(info));
		}

		[Fact]
		public void Empty_fields_are_dropped() {
			var info = new CardInfo("Plain Card", "", "", "");
			Assert.Equal("Plain Card", Describe(info));
		}

		[Fact]
		public void Completed_encounter_announces_status_before_stat() {
			var info = new CardInfo(
				title: "Ambush",
				description: "Bandits attack.",
				statValueString: "",
				valueString: "",
				complete: true);
			Assert.Equal("Ambush, completed, Bandits attack.", Describe(info));
		}

		[Fact]
		public void Incomplete_encounter_omits_status() {
			var info = new CardInfo("Ambush", "Bandits attack.", "", "");
			Assert.Equal("Ambush, Bandits attack.", Describe(info));
		}

		[Fact]
		public void Token_is_announced_when_winnable() {
			// Only that a token can be won; the reward cards are never read.
			var info = new CardInfo("Demon Trader III", "", "", "", hasToken: true);
			Assert.Equal("Demon Trader III, token available", Describe(info));
		}

		[Fact]
		public void Token_is_omitted_when_not_winnable() {
			var info = new CardInfo("Goblin", "Deal damage", "", "", hasToken: false);
			Assert.Equal("Goblin, Deal damage", Describe(info));
		}

		[Fact]
		public void Description_duplicating_title_is_spoken_once() {
			// Message dedup collapses a description that filters to the same text as
			// the title; the player still hears the distinguishing stat.
			var info = new CardInfo("Gold", "Gold", "Gold: 5", "");
			Assert.Equal("Gold, Gold: 5", Describe(info));
		}

		[Fact]
		public void Card_text_markup_is_stripped() {
			var info = new CardInfo("[b]Goblin[/b]", "[FF0000]Deal 5[-]", "", "");
			Assert.Equal("Goblin, Deal 5", Describe(info));
		}

		[Fact]
		public void Equipment_traits_follow_description_before_value() {
			var info = new CardInfo(
				title: "War Axe",
				description: "A heavy blade.",
				statValueString: "Damage: 8",
				valueString: "Value: 20 gold",
				traits: "Two-handed, Slow");
			Assert.Equal("War Axe, Damage: 8, A heavy blade., Two-handed, Slow, Value: 20 gold", Describe(info));
		}

		[Fact]
		public void No_traits_field_leaves_readout_unchanged() {
			var info = new CardInfo("Iron Sword", "A reliable blade.", "Damage: 5", "");
			Assert.Equal("Iron Sword, Damage: 5, A reliable blade.", Describe(info));
		}

		[Fact]
		public void New_badge_announces_new_after_title() {
			var info = new CardInfo("Iron Sword", "A reliable blade.", "Damage: 5", "", isNew: true);
			Assert.Equal("Iron Sword, " + Strings.CardNew + ", Damage: 5, A reliable blade.", Describe(info));
		}

		[Fact]
		public void Pinned_card_announces_cannot_remove() {
			var info = new CardInfo("Sword", "A blade.", "Damage: 5", "", pinned: true);
			Assert.Equal("Sword, " + Strings.CardPinned + ", Damage: 5, A blade.", Describe(info));
		}

		[Fact]
		public void New_and_pinned_both_read_new_first() {
			var info = new CardInfo("Sword", "", "", "", isNew: true, pinned: true);
			Assert.Equal("Sword, " + Strings.CardNew + ", " + Strings.CardPinned, Describe(info));
		}

		[Fact]
		public void Unflagged_card_omits_new_and_pinned() {
			var info = new CardInfo("Sword", "A blade.", "Damage: 5", "");
			Assert.Equal("Sword, Damage: 5, A blade.", Describe(info));
		}

		[Fact]
		public void Charges_follow_stat_before_rules() {
			var info = new CardInfo("Throwing Axe", "Hurl at a foe.", "Damage: 4", "", charges: 3);
			Assert.Equal("Throwing Axe, Damage: 4, 3 " + Strings.CardCharges + ", Hurl at a foe.", Describe(info));
		}

		[Fact]
		public void Single_charge_reads_singular() {
			var info = new CardInfo("Bomb", "Explodes.", "", "", charges: 1);
			Assert.Equal("Bomb, 1 " + Strings.CardCharge + ", Explodes.", Describe(info));
		}

		[Fact]
		public void Artifact_with_no_stat_leads_with_charges() {
			// Artifacts carry no damage/defence stat, so the charge count is their key number.
			var info = new CardInfo("Holy Grenade", "Smite a foe.", "", "", charges: 2);
			Assert.Equal("Holy Grenade, 2 " + Strings.CardCharges + ", Smite a foe.", Describe(info));
		}

		[Fact]
		public void Zero_charges_still_reads() {
			// The game shows the counter at 0 (a spent consumable); -1 is the hidden case.
			var info = new CardInfo("Bomb", "", "", "", charges: 0);
			Assert.Equal("Bomb, 0 " + Strings.CardCharges, Describe(info));
		}

		[Fact]
		public void No_charges_when_negative() {
			var info = new CardInfo("Sword", "A blade.", "Damage: 5", "", charges: -1);
			Assert.Equal("Sword, Damage: 5, A blade.", Describe(info));
		}

		[Fact]
		public void Face_down_card_withholds_identity() {
			// A face-down card (a locked cabinet card, say) is the card back a sighted
			// player sees: only "face down card" is spoken, never the withheld details.
			var info = new CardInfo("Lich King", "Final boss.", "Damage: 99", "", faceDown: true);
			Assert.Equal("face down card", Describe(info));
		}

		[Fact]
		public void Insufficient_follows_the_value() {
			// In a shop the value line is the live price; the insufficient-funds warning
			// reads right after it so cost and affordability are one unit.
			var info = new CardInfo("War Axe", "A heavy blade.", "Damage: 8", "$84",
				insufficient: "Insufficient Funds");
			Assert.Equal("War Axe, Damage: 8, A heavy blade., $84, Insufficient Funds", Describe(info));
		}

		[Fact]
		public void Affordable_card_omits_the_warning() {
			var info = new CardInfo("War Axe", "A heavy blade.", "Damage: 8", "$84");
			Assert.Equal("War Axe, Damage: 8, A heavy blade., $84", Describe(info));
		}

		[Fact]
		public void Face_down_shop_card_reads_price_but_not_identity() {
			// An unseen shop stock card lies face-down, but the shop's panel still shows
			// its price (and affordability), so those speak; the identity stays withheld.
			var info = new CardInfo("Mystery Blade", "Sharp.", "Damage: 9", "", faceDown: true)
				.WithShopPrice("$120", "Insufficient Funds");
			Assert.Equal("face down card, $120, Insufficient Funds", Describe(info));
		}

		[Fact]
		public void Shop_price_replaces_the_face_value() {
			// The card face prints the base value; the shop charges value times a stock
			// multiplier. Only the panel's figure speaks, so the numbers never contradict.
			var info = new CardInfo("War Axe", "A heavy blade.", "Damage: 8", "$70",
				traits: "Two-handed", charges: 2, isNew: true)
				.WithShopPrice("$84", null);
			Assert.Equal("War Axe, " + Strings.CardNew + ", Damage: 8, 2 " + Strings.CardCharges
				+ ", A heavy blade., Two-handed, $84", Describe(info));
		}
	}
}
