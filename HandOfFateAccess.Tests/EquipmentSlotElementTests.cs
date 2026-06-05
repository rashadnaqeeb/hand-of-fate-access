using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// An empty paperdoll equipment slot: category first, then that it is empty.
	/// Covers the silent-failure surface where a sprite-only slot would otherwise
	/// speak its bare object name.
	/// </summary>
	public class EquipmentSlotElementTests {
		private static string Describe(string category) =>
			new EquipmentSlotElement(category).Describe().Resolve();

		[Fact]
		public void Category_then_empty() {
			Assert.Equal("Weapons, " + Strings.SlotEmpty, Describe("Weapons"));
		}

		[Fact]
		public void Missing_category_speaks_only_empty() {
			// The game's category lookup returns no title for an unknown slot type; the
			// slot is still meaningfully announced as empty rather than going silent.
			Assert.Equal(Strings.SlotEmpty, Describe(""));
		}

		[Fact]
		public void Null_category_speaks_only_empty() {
			Assert.Equal(Strings.SlotEmpty, Describe(null));
		}
	}
}
