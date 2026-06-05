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
	}
}
