using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The equipment-replace prompt readout: "Equip {new}, replacing {old}", each item as
	/// name, stat (title and value rejoined), then rules text. Covers the display-only
	/// comparison the focus model never reaches.
	/// </summary>
	public class CompareNarrationTests {
		private static CompareItem Item(string title, string statTitle, string statValue, string description) =>
			new CompareItem { Title = title, StatTitle = statTitle, StatValue = statValue, Description = description };

		[Fact]
		public void Equip_new_replacing_old() {
			string result = CompareNarration.Compose(
				Item("Iron Sword", "Damage", "5", "A reliable blade."),
				Item("Rusty Dagger", "Damage", "3", "Worn and dull."));
			Assert.Equal(
				"Equip Iron Sword, Damage 5, A reliable blade., replacing Rusty Dagger, Damage 3, Worn and dull.",
				result);
		}

		[Fact]
		public void Stat_title_and_value_read_together() {
			string result = CompareNarration.Compose(
				Item("War Axe", "Damage", "8", null),
				Item("Club", "Damage", "4", null));
			Assert.Equal("Equip War Axe, Damage 8, replacing Club, Damage 4", result);
		}

		[Fact]
		public void Missing_stat_title_or_value_does_not_leave_stray_text() {
			string result = CompareNarration.Compose(
				Item("Helm", "", "2", null),
				Item("Cap", "Armour", "", null));
			Assert.Equal("Equip Helm, 2, replacing Cap, Armour", result);
		}

		[Fact]
		public void Markup_stripped_in_items() {
			string result = CompareNarration.Compose(
				Item("[b]Shield[/b]", "Block", "10", null),
				Item("Buckler", "Block", "5", null));
			Assert.Equal("Equip Shield, Block 10, replacing Buckler, Block 5", result);
		}

		[Fact]
		public void Empty_when_either_item_has_no_text() {
			Assert.Equal("", CompareNarration.Compose(Item("Sword", "Damage", "5", null), Item(null, null, null, null)));
			Assert.Equal("", CompareNarration.Compose(null, null));
		}
	}
}
