using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The composition kernel: parts go in raw and in order; filtering, empty-drop,
	/// dedup, and joining all happen at Resolve.
	/// </summary>
	public class MessageTests {
		[Fact]
		public void Empty_message_resolves_to_empty() {
			Assert.Equal("", new Message().Resolve());
		}

		[Fact]
		public void Parts_joined_in_order() {
			string result = new Message().Add("Goblin").Add("Deal 5 damage").Resolve();
			Assert.Equal("Goblin, Deal 5 damage", result);
		}

		[Fact]
		public void Null_and_empty_parts_ignored_on_add() {
			string result = new Message().Add(null).Add("").Add("Title").Resolve();
			Assert.Equal("Title", result);
		}

		[Fact]
		public void Markup_stripped_at_resolve() {
			Assert.Equal("Gold: 5", new Message().Add("[FFAA00]Gold:[-] [b]5[/b]").Resolve());
		}

		[Fact]
		public void Parts_that_filter_to_empty_are_dropped() {
			Assert.Equal("Real", new Message().Add("[b][/b]").Add("   ").Add("Real").Resolve());
		}

		[Fact]
		public void Duplicate_filtered_parts_collapsed() {
			Assert.Equal("Continue", new Message().Add("Continue").Add("Continue").Resolve());
		}

		[Fact]
		public void Dedup_compares_filtered_text_not_raw() {
			// Different markup, same spoken text, collapses to one.
			string result = new Message().Add("[b]Gold[/b]").Add("Gold").Resolve();
			Assert.Equal("Gold", result);
		}

		[Fact]
		public void AddRange_appends_in_order() {
			string result = new Message().Add("Title").AddRange(new[] { "a", "b" }).Resolve();
			Assert.Equal("Title, a, b", result);
		}

		[Fact]
		public void AddRange_null_is_safe() {
			Assert.Equal("Title", new Message().Add("Title").AddRange(null).Resolve());
		}

		[Fact]
		public void Custom_separator_used() {
			Assert.Equal("a | b", new Message().Add("a").Add("b").Resolve(" | "));
		}
	}
}
