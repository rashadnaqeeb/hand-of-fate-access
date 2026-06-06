using HandOfFateAccess.Localization;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The card-zoom readout: prompt, card, the item a replace swaps out, and lore in the
	/// decision line; the button labels in the hint. A face-down card withholds its
	/// identity. Covers the zoom overlay the focus model cannot reach.
	/// </summary>
	public class ZoomNarrationTests {
		private static CardInfo Card(string title, string stat, string desc) =>
			new CardInfo(title, desc, stat, "");

		[Fact]
		public void Examine_reads_card_then_hint() {
			var z = new ZoomInfo { Card = Card("Iron Sword", "Damage: 5", "A blade."), Confirm = "Continue" };
			var a = ZoomNarration.Compose(z);
			Assert.Equal("Iron Sword, Damage: 5, A blade.", a.Main);
			Assert.Equal("Continue", a.Hint);
		}

		[Fact]
		public void Title_leads_the_decision_line() {
			var z = new ZoomInfo { Title = "Keep this card?", Card = Card("Bandit", "", ""), Confirm = "Keep", Cancel = "Back" };
			var a = ZoomNarration.Compose(z);
			Assert.Equal("Keep this card?, Bandit", a.Main);
			Assert.Equal("Keep, Back", a.Hint);
		}

		[Fact]
		public void Replace_includes_the_old_item() {
			var z = new ZoomInfo {
				Title = "Equip this?",
				Card = Card("Iron Sword", "Damage: 5", ""),
				OldItem = new CompareItem { Title = "Rusty Dagger", StatTitle = "Damage", StatValue = "3" },
				Confirm = "Yes",
				Cancel = "No",
			};
			var a = ZoomNarration.Compose(z);
			Assert.Equal("Equip this?, Iron Sword, Damage: 5, " + Strings.ZoomReplacing + " Rusty Dagger, Damage 3", a.Main);
			Assert.Equal("Yes, No", a.Hint);
		}

		[Fact]
		public void Lore_is_appended() {
			var z = new ZoomInfo { Card = Card("Dealer", "", ""), Lore = "A dark tale.", Confirm = "Continue" };
			Assert.Equal("Dealer, A dark tale.", ZoomNarration.Compose(z).Main);
		}

		[Fact]
		public void Flipped_reads_face_down_only() {
			var z = new ZoomInfo { Flipped = true, Card = null, Confirm = "Reveal" };
			var a = ZoomNarration.Compose(z);
			Assert.Equal(Strings.CardFaceDown, a.Main);
			Assert.Equal("Reveal", a.Hint);
		}

		[Fact]
		public void Hint_is_confirm_only_when_no_cancel() {
			var z = new ZoomInfo { Card = Card("Blessing", "", ""), Confirm = "Continue", Cancel = null };
			Assert.Equal("Continue", ZoomNarration.Compose(z).Hint);
		}

		[Fact]
		public void Null_zoom_is_empty() {
			var a = ZoomNarration.Compose(null);
			Assert.Equal("", a.Main);
			Assert.Equal("", a.Hint);
		}
	}
}
