using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class MonsterNarrationTests {
		[Fact]
		public void Title_NumberCard_JoinsCountConnectorCreature() {
			Assert.Equal("3 of Ratmen", MonsterNarration.Title("3", "of", "Ratmen"));
		}

		[Fact]
		public void Title_FaceCard_JoinsRankConnectorCreature() {
			Assert.Equal("Queen of Skeletons", MonsterNarration.Title("Queen", "of", "Skeletons"));
		}

		[Fact]
		public void Title_DropsEmptyPieces() {
			Assert.Equal("Ratmen", MonsterNarration.Title("", "", "Ratmen"));
			Assert.Equal("3 Ratmen", MonsterNarration.Title("3", null, "Ratmen"));
		}

		[Fact]
		public void Roster_JoinsTitlesInOrder() {
			Assert.Equal(
				"Queen of Skeletons, 3 of Ratmen",
				MonsterNarration.Roster(new[] { "Queen of Skeletons", "3 of Ratmen" }));
		}

		[Fact]
		public void Roster_KeepsDuplicates() {
			Assert.Equal(
				"3 of Ratmen, 3 of Ratmen",
				MonsterNarration.Roster(new[] { "3 of Ratmen", "3 of Ratmen" }));
		}

		[Fact]
		public void Roster_DropsEmptyEntries() {
			Assert.Equal(
				"3 of Ratmen",
				MonsterNarration.Roster(new[] { "", "3 of Ratmen", null }));
		}

		[Fact]
		public void Roster_EmptyWhenNoneStaged() {
			Assert.Equal("", MonsterNarration.Roster(null));
			Assert.Equal("", MonsterNarration.Roster(new string[0]));
		}
	}
}
