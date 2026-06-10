using HandOfFateAccess.Glossary;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class GlossaryMenuTests {
		private static GlossaryEntry Entry(string label) =>
			new GlossaryEntry(label, new GlossaryStep[0]);

		private static GlossaryMenu Menu(params string[] labels) {
			var entries = new GlossaryEntry[labels.Length];
			for (int i = 0; i < labels.Length; i++)
				entries[i] = Entry(labels[i]);
			return new GlossaryMenu(entries);
		}

		[Fact]
		public void Current_StartsAtFirstEntry() {
			Assert.Equal("a", Menu("a", "b", "c").Current.Label);
		}

		[Fact]
		public void MoveNext_WalksForwardAndWraps() {
			var menu = Menu("a", "b", "c");
			Assert.Equal("b", menu.MoveNext().Label);
			Assert.Equal("c", menu.MoveNext().Label);
			Assert.Equal("a", menu.MoveNext().Label);
		}

		[Fact]
		public void MovePrevious_FromFirst_WrapsToLast() {
			var menu = Menu("a", "b", "c");
			Assert.Equal("c", menu.MovePrevious().Label);
			Assert.Equal("b", menu.MovePrevious().Label);
		}

		[Fact]
		public void Reset_ReturnsToFirstEntry() {
			var menu = Menu("a", "b", "c");
			menu.MoveNext();
			menu.MoveNext();
			menu.Reset();
			Assert.Equal("a", menu.Current.Label);
		}
	}
}
