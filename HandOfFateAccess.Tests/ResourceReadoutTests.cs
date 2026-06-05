using HandOfFateAccess.Localization;
using HandOfFateAccess.Resources;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The on-demand resource readout and live-change formatting: relevance order,
	/// health as current of max, and resources hidden when the run does not use them.
	/// </summary>
	public class ResourceReadoutTests {
		[Fact]
		public void Health_first_then_food_then_gold() {
			var s = new ResourceSnapshot {
				HasHealth = true, Health = 200, MaxHealth = 200,
				HasFood = true, Food = 5,
				HasGold = true, Gold = 3,
			};
			Assert.Equal("200/200 health, 5 food, 3 gold", ResourceReadout.Compose(s));
		}

		[Fact]
		public void Health_reads_current_of_max() {
			var s = new ResourceSnapshot { HasHealth = true, Health = 12, MaxHealth = 20 };
			Assert.Equal("12/20 health", ResourceReadout.Compose(s));
		}

		[Fact]
		public void Hidden_resources_are_omitted() {
			// Iron ore not used this run (no stat card), so it is left out, not read as 0.
			var s = new ResourceSnapshot {
				HasHealth = true, Health = 10, MaxHealth = 10,
				HasGold = true, Gold = 0,
				HasIronOre = false, IronOre = 0,
			};
			Assert.Equal("10/10 health, 0 gold", ResourceReadout.Compose(s));
		}

		[Fact]
		public void Iron_ore_and_tokens_shown_when_present() {
			var s = new ResourceSnapshot {
				HasHealth = true, Health = 8, MaxHealth = 8,
				HasIronOre = true, IronOre = 4,
				Tokens = 2,
			};
			Assert.Equal("8/8 health, 4 iron ore, 2 tokens", ResourceReadout.Compose(s));
		}

		[Fact]
		public void Tokens_omitted_when_none() {
			var s = new ResourceSnapshot { HasHealth = true, Health = 1, MaxHealth = 1, Tokens = 0 };
			Assert.Equal("1/1 health", ResourceReadout.Compose(s));
		}

		[Fact]
		public void Empty_snapshot_reads_nothing() {
			Assert.Equal("", ResourceReadout.Compose(new ResourceSnapshot()));
			Assert.Equal("", ResourceReadout.Compose(null));
		}

		[Fact]
		public void Delta_gain_has_plus_sign() {
			Assert.Equal("+3 " + Strings.ResourceGold, ResourceText.Delta(ResourceKind.Gold, 3));
		}

		[Fact]
		public void Delta_loss_has_minus_sign() {
			Assert.Equal("-5 " + Strings.ResourceHealth, ResourceText.Delta(ResourceKind.Health, -5));
		}

		[Fact]
		public void Delta_max_health_uses_its_own_noun() {
			Assert.Equal("+10 " + Strings.ResourceMaxHealth, ResourceText.Delta(ResourceKind.MaxHealth, 10));
		}

		[Fact]
		public void Delta_zero_is_empty() {
			Assert.Equal("", ResourceText.Delta(ResourceKind.Food, 0));
		}
	}
}
