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
		public void Hint_pairs_each_label_with_its_bound_key() {
			var z = new ZoomInfo {
				Card = Card("Bandit", "", ""),
				Confirm = "Keep", ConfirmKey = "A",
				Cancel = "Back", CancelKey = "B",
			};
			Assert.Equal("Keep: A, Back: B", ZoomNarration.Compose(z).Hint);
		}

		[Fact]
		public void Hint_label_stands_alone_when_its_key_is_unknown() {
			var z = new ZoomInfo {
				Card = Card("Bandit", "", ""),
				Confirm = "Keep", ConfirmKey = "Enter Key",
				Cancel = "Back",
			};
			Assert.Equal("Keep: Enter Key, Back", ZoomNarration.Compose(z).Hint);
		}

		[Fact]
		public void Hint_ignores_a_key_without_an_action() {
			var z = new ZoomInfo { Card = Card("Blessing", "", ""), Confirm = "Continue", CancelKey = "B" };
			Assert.Equal("Continue", ZoomNarration.Compose(z).Hint);
		}

		[Fact]
		public void Null_zoom_is_empty() {
			var a = ZoomNarration.Compose(null);
			Assert.Equal("", a.Main);
			Assert.Equal("", a.Hint);
		}

		[Fact]
		public void Shop_prompt_leads_the_decision_line() {
			// The game's transaction sentence carries the real price, so it reads first
			// and the player can act on it before the card detail re-reads.
			var z = new ZoomInfo {
				ShopPrompt = "Buy War Axe for 84 gold?",
				Card = Card("War Axe", "Damage: 8", ""),
				Confirm = "Buy", Cancel = "Back",
			};
			var a = ZoomNarration.Compose(z);
			Assert.Equal("Buy War Axe for 84 gold?, War Axe, Damage: 8", a.Main);
			Assert.Equal("Buy, Back", a.Hint);
		}

		[Fact]
		public void Shop_insufficient_follows_the_prompt() {
			// When the player cannot afford the item the game offers no confirm action;
			// the warning right after the prompt says why.
			var z = new ZoomInfo {
				ShopPrompt = "Buy War Axe for 84 gold?",
				ShopInsufficient = "Insufficient Funds",
				Card = Card("War Axe", "Damage: 8", ""),
				Cancel = "Back",
			};
			var a = ZoomNarration.Compose(z);
			Assert.Equal("Buy War Axe for 84 gold?, Insufficient Funds, War Axe, Damage: 8", a.Main);
			Assert.Equal("Back", a.Hint);
		}

		[Fact]
		public void Flipped_zoom_never_reads_the_shop_prompt() {
			// The sentence names the card; a face-down card's reveal decision must not
			// leak it even if a stale prompt were supplied.
			var z = new ZoomInfo {
				Flipped = true,
				ShopPrompt = "Buy War Axe for 84 gold?",
				Confirm = "Reveal",
			};
			Assert.Equal(Strings.CardFaceDown, ZoomNarration.Compose(z).Main);
		}
	}
}
