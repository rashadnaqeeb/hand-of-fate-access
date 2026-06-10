using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The whole-set card choice (keep all the dealt cards, or redraw the set): the
	/// prompt leads, then every card in the set as a normal card readout, then the
	/// confirm and cancel actions with their bound keys, since the choice has no
	/// navigable buttons through which to discover which input takes which action.
	/// </summary>
	public class ChoiceSetElementTests {
		private static CardInfo Card(string title, string description = null) =>
			new CardInfo(title, description, null, null);

		[Fact]
		public void Prompt_cards_then_actions() {
			string text = new ChoiceSetElement(
				"Keep or redraw these cards?",
				new[] { Card("Apple", "Restores 5 food"), Card("Bread") },
				"Keep", "A", "Redraw", "B").Describe().Resolve();
			Assert.Equal("Keep or redraw these cards?, Apple, Restores 5 food, Bread, Keep: A, Redraw: B", text);
		}

		[Fact]
		public void Missing_key_names_leave_bare_action_labels() {
			string text = new ChoiceSetElement(
				"Keep or redraw these cards?", new[] { Card("Apple") },
				"Keep", null, "Redraw", null).Describe().Resolve();
			Assert.Equal("Keep or redraw these cards?, Apple, Keep, Redraw", text);
		}

		[Fact]
		public void Lone_action_reads_without_its_key() {
			// With a single option there is no other input to tell apart, so the key is noise.
			string text = new ChoiceSetElement(
				"Keep these cards?", new[] { Card("Apple") },
				"Keep", "Return", null, null).Describe().Resolve();
			Assert.Equal("Keep these cards?, Apple, Keep", text);
		}

		[Fact]
		public void Empty_prompt_and_actions_drop_out() {
			string text = new ChoiceSetElement(
				null, new[] { Card("Apple") }, null, null, "", null).Describe().Resolve();
			Assert.Equal("Apple", text);
		}

		[Fact]
		public void Null_card_list_reads_actions_alone() {
			string text = new ChoiceSetElement(
				"Keep or redraw these cards?", null, "Keep", "A", "Redraw", "B").Describe().Resolve();
			Assert.Equal("Keep or redraw these cards?, Keep: A, Redraw: B", text);
		}
	}
}
