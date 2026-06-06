using System.Collections.Generic;
using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// A paperdoll equipment slot: category first, then each equipped card, or "empty"
	/// when nothing is equipped. Covers the silent-failure surface where a sprite-only
	/// slot would otherwise speak its bare object name, including the filled case where
	/// the equipped card sits as a child the focus path cannot reach.
	/// </summary>
	public class EquipmentSlotElementTests {
		private static string Empty(string category) =>
			new EquipmentSlotElement(category, new List<CardInfo>()).Describe().Resolve();

		private static string Filled(string category, params CardInfo[] cards) =>
			new EquipmentSlotElement(category, new List<CardInfo>(cards)).Describe().Resolve();

		[Fact]
		public void Category_then_empty() {
			Assert.Equal("Weapons, " + Strings.SlotEmpty, Empty("Weapons"));
		}

		[Fact]
		public void Missing_category_speaks_only_empty() {
			// The game's category lookup returns no title for an unknown slot type; the
			// slot is still meaningfully announced as empty rather than going silent.
			Assert.Equal(Strings.SlotEmpty, Empty(""));
		}

		[Fact]
		public void Null_category_speaks_only_empty() {
			Assert.Equal(Strings.SlotEmpty, Empty(null));
		}

		[Fact]
		public void Filled_slot_reads_category_then_equipped_card() {
			var sword = new CardInfo("Iron Sword", "A reliable blade.", "Damage: 5", "");
			Assert.Equal("Weapons, Iron Sword, Damage: 5, A reliable blade.", Filled("Weapons", sword));
		}

		[Fact]
		public void Multi_card_slot_reads_each_equipped_card() {
			// Trinket and modifier slots hold several cards; every one is read.
			var coin = new CardInfo("Lucky Coin", null, null, "");
			var foot = new CardInfo("Rabbit Foot", null, null, "");
			Assert.Equal("Trinkets, Lucky Coin, Rabbit Foot", Filled("Trinkets", coin, foot));
		}
	}
}
