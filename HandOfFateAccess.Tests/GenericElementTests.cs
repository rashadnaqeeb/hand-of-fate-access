using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The generic fallback's readout: every label, markup stripped, empties
	/// dropped, duplicates collapsed, object name only when no label is usable.
	/// Ported from the Phase 2 FocusComposer suite - same silent-failure surfaces.
	/// </summary>
	public class GenericElementTests {
		private static string Describe(string name, string[] labels) =>
			new GenericElement(name, labels).Describe().Resolve();

		[Fact]
		public void Single_label_spoken_verbatim() {
			Assert.Equal("New Game", Describe("NewGameButton", new[] { "New Game" }));
		}

		[Fact]
		public void Multiple_child_labels_joined() {
			Assert.Equal("Goblin, Deal 5 damage", Describe("Card", new[] { "Goblin", "Deal 5 damage" }));
		}

		[Fact]
		public void Markup_stripped_through_text_filter() {
			Assert.Equal("Gold: 5", Describe("Card", new[] { "[FFAA00]Gold:[-] [b]5[/b]" }));
		}

		[Fact]
		public void Empty_and_whitespace_labels_dropped() {
			Assert.Equal("Title", Describe("Card", new[] { "", "   ", "Title" }));
		}

		[Fact]
		public void Markup_only_labels_dropped() {
			Assert.Equal("Real text", Describe("Card", new[] { "[b][/b]", "Real text" }));
		}

		[Fact]
		public void Duplicate_labels_collapsed() {
			// NGUI shadow/outline copies show up as identical label text.
			Assert.Equal("Continue", Describe("Button", new[] { "Continue", "Continue" }));
		}

		[Fact]
		public void Falls_back_to_name_when_no_labels() {
			Assert.Equal("Settings", Describe("Settings", new string[0]));
		}

		[Fact]
		public void Falls_back_to_name_when_labels_all_empty() {
			Assert.Equal("Settings", Describe("Settings", new[] { "", "[i][/i]" }));
		}

		[Fact]
		public void Empty_when_no_labels_and_no_name() {
			Assert.Equal("", Describe("", new string[0]));
		}

		[Fact]
		public void Null_name_and_null_labels_is_empty() {
			Assert.Equal("", Describe(null, null));
		}

		[Fact]
		public void Markup_only_name_fallback_is_empty() {
			// Name is non-empty raw but filters to nothing; no usable text to speak.
			Assert.Equal("", Describe("[b][/b]", new string[0]));
		}
	}
}
