using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The encounter choice readout: number first (so a gap signals a focus-skipped
	/// disabled choice), then the choice text, with the decorative ")" trimmed.
	/// </summary>
	public class ChoiceElementTests {
		private static string Describe(ChoiceInfo info) =>
			new ChoiceElement(info).Describe().Resolve();

		[Fact]
		public void Number_first_then_text() {
			Assert.Equal(
				"1, Fight the bandits (75% chance of success)",
				Describe(new ChoiceInfo("1)", "Fight the bandits (75% chance of success)")));
		}

		[Fact]
		public void Decorative_paren_on_number_is_trimmed() {
			Assert.Equal("2, Flee", Describe(new ChoiceInfo("2)", "Flee")));
		}

		[Fact]
		public void Text_only_when_no_number() {
			Assert.Equal("Leave", Describe(new ChoiceInfo(null, "Leave")));
			Assert.Equal("Leave", Describe(new ChoiceInfo("", "Leave")));
		}

		[Fact]
		public void Markup_is_stripped() {
			Assert.Equal("1, Deal 5", Describe(new ChoiceInfo("1)", "[b]Deal[/b] [FF0000]5[-]")));
		}
	}
}
