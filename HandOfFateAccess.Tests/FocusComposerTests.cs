using HandOfFateAccess.Focus;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class FocusComposerTests {
		[Fact]
		public void Null_dto_is_empty() {
			Assert.Equal("", FocusComposer.Compose(null));
		}

		[Fact]
		public void Single_label_spoken_verbatim() {
			var dto = new FocusDto("NewGameButton", new[] { "New Game" });
			Assert.Equal("New Game", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Multiple_child_labels_joined() {
			var dto = new FocusDto("Card", new[] { "Goblin", "Deal 5 damage" });
			Assert.Equal("Goblin, Deal 5 damage", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Markup_stripped_through_text_filter() {
			var dto = new FocusDto("Card", new[] { "[FFAA00]Gold:[-] [b]5[/b]" });
			Assert.Equal("Gold: 5", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Empty_and_whitespace_labels_dropped() {
			var dto = new FocusDto("Card", new[] { "", "   ", "Title" });
			Assert.Equal("Title", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Markup_only_labels_dropped() {
			var dto = new FocusDto("Card", new[] { "[b][/b]", "Real text" });
			Assert.Equal("Real text", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Duplicate_labels_collapsed() {
			// NGUI shadow/outline copies show up as identical label text.
			var dto = new FocusDto("Button", new[] { "Continue", "Continue" });
			Assert.Equal("Continue", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Falls_back_to_name_when_no_labels() {
			var dto = new FocusDto("Settings", new string[0]);
			Assert.Equal("Settings", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Falls_back_to_name_when_labels_all_empty() {
			var dto = new FocusDto("Settings", new[] { "", "[i][/i]" });
			Assert.Equal("Settings", FocusComposer.Compose(dto));
		}

		[Fact]
		public void Empty_when_no_labels_and_no_name() {
			var dto = new FocusDto("", new string[0]);
			Assert.Equal("", FocusComposer.Compose(dto));
		}
	}
}
