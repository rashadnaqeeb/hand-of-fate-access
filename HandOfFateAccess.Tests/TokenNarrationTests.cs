using HandOfFateAccess.Localization;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// Humanising a reward token's object id into a spoken name: strip the "Token_"
	/// prefix and "(Clone)" suffix, split at camel-case and letter/digit boundaries, and
	/// keep digits as part of the name ("Token_WhiteMinotaur5(Clone)" -> "White Minotaur 5").
	/// </summary>
	public class TokenNarrationTests {
		[Fact]
		public void Strips_affixes_and_splits_words_keeping_the_number() {
			Assert.Equal("White Minotaur 5", TokenNarration.Name("Token_WhiteMinotaur5(Clone)"));
		}

		[Fact]
		public void Splits_a_bare_camel_case_id() {
			Assert.Equal("White Minotaur", TokenNarration.Name("WhiteMinotaur"));
		}

		[Fact]
		public void Keeps_a_trailing_number_as_its_own_word() {
			Assert.Equal("Goblin 12", TokenNarration.Name("Goblin12"));
		}

		[Fact]
		public void Splits_a_number_followed_by_a_word() {
			Assert.Equal("3 Eyed Beast", TokenNarration.Name("3EyedBeast"));
		}

		[Fact]
		public void Keeps_an_acronym_run_together() {
			Assert.Equal("XML Boss", TokenNarration.Name("XMLBoss"));
		}

		[Fact]
		public void Underscores_become_single_spaces() {
			Assert.Equal("Old King", TokenNarration.Name("Old_King"));
		}

		[Fact]
		public void Empty_id_yields_empty() {
			Assert.Equal("", TokenNarration.Name(""));
		}

		[Fact]
		public void Compose_speaks_the_localized_title_verbatim() {
			// The granting card's title already carries the tier number, so nothing is appended.
			Assert.Equal("White Minotaur 5", TokenNarration.Compose("White Minotaur 5", "Token_WhiteMinotaur5(Clone)"));
		}

		[Fact]
		public void Compose_falls_back_to_the_synthesized_name_without_a_title() {
			Assert.Equal("White Minotaur 5", TokenNarration.Compose(null, "Token_WhiteMinotaur5(Clone)"));
		}

		[Fact]
		public void Element_speaks_localized_title_then_the_type_word() {
			string spoken = new RewardTokenElement("White Minotaur 5", "Token_WhiteMinotaur5(Clone)").Describe().Resolve();
			Assert.Equal("White Minotaur 5, " + Strings.TokenReward, spoken);
		}

		[Fact]
		public void Element_without_a_title_synthesizes_from_the_id() {
			string spoken = new RewardTokenElement(null, "Token_WhiteMinotaur5(Clone)").Describe().Resolve();
			Assert.Equal("White Minotaur 5, " + Strings.TokenReward, spoken);
		}

		[Fact]
		public void Element_with_no_title_and_empty_id_speaks_only_the_type_word() {
			string spoken = new RewardTokenElement(null, "").Describe().Resolve();
			Assert.Equal(Strings.TokenReward, spoken);
		}
	}
}
