using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The targeted card readout: title first, then status, stat, rules/prompt,
	/// value, token stakes - with empty model fields dropping out of the line.
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
				tokens: null,
				complete: true);
			Assert.Equal("Ambush, completed, Bandits attack.", Describe(info));
		}

		[Fact]
		public void Incomplete_encounter_omits_status() {
			var info = new CardInfo("Ambush", "Bandits attack.", "", "", tokens: null, complete: false);
			Assert.Equal("Ambush, Bandits attack.", Describe(info));
		}

		[Fact]
		public void Encounter_tokens_read_after_prompt() {
			var info = new CardInfo(
				title: "Gamble",
				description: "Place your bet.",
				statValueString: "",
				valueString: "",
				tokens: new[] { new TokenStake("Gold", ""), new TokenStake("", "Curse") });
			Assert.Equal("Gamble, Place your bet., gain Gold, lose Curse", Describe(info));
		}

		[Fact]
		public void Token_stake_with_both_sides_reads_gain_then_lose() {
			var info = new CardInfo("Trade", "", "", "",
				tokens: new[] { new TokenStake("Sword", "Gold") });
			Assert.Equal("Trade, gain Sword, lose Gold", Describe(info));
		}

		[Fact]
		public void Token_stake_empty_side_is_dropped() {
			// An empty gain/remove must not produce a bare "gain"/"lose".
			var info = new CardInfo("Gift", "", "", "",
				tokens: new[] { new TokenStake("Bread", "") });
			Assert.Equal("Gift, gain Bread", Describe(info));
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
	}
}
