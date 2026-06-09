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
		public void Roster_DropsTitleTheFilterReducesToEmpty() {
			Assert.Equal(
				"3 of Ratmen",
				MonsterNarration.Roster(new[] { "[FF0000][-]", "3 of Ratmen", "   " }));
		}

		[Fact]
		public void Roster_EmptyWhenNoneStaged() {
			Assert.Equal("", MonsterNarration.Roster(null));
			Assert.Equal("", MonsterNarration.Roster(new string[0]));
		}

		[Fact]
		public void RosterStep_SpeaksEachCardOnceAsDealt() {
			int spoken = 0;
			Assert.Equal("2 of Dust", MonsterNarration.RosterStep(new[] { "2 of Dust" }, ref spoken));
			Assert.Equal("2 of Skulls", MonsterNarration.RosterStep(new[] { "2 of Dust", "2 of Skulls" }, ref spoken));
			Assert.Equal("", MonsterNarration.RosterStep(new[] { "2 of Dust", "2 of Skulls" }, ref spoken));
		}

		[Fact]
		public void RosterStep_JoinsCardsLandingTogether() {
			int spoken = 0;
			Assert.Equal(
				"Queen of Skeletons, 3 of Ratmen",
				MonsterNarration.RosterStep(new[] { "Queen of Skeletons", "3 of Ratmen" }, ref spoken));
			Assert.Equal(2, spoken);
		}

		[Fact]
		public void RosterStep_DuplicateCardSpeaksAgain() {
			int spoken = 0;
			Assert.Equal("3 of Ratmen", MonsterNarration.RosterStep(new[] { "3 of Ratmen" }, ref spoken));
			Assert.Equal("3 of Ratmen", MonsterNarration.RosterStep(new[] { "3 of Ratmen", "3 of Ratmen" }, ref spoken));
		}

		[Fact]
		public void RosterStep_ShrinkClampsSilently() {
			int spoken = 2;
			Assert.Equal("", MonsterNarration.RosterStep(new[] { "2 of Dust" }, ref spoken));
			Assert.Equal(1, spoken);
		}

		[Fact]
		public void RosterStep_NullReadResetsForNextEncounter() {
			int spoken = 2;
			Assert.Equal("", MonsterNarration.RosterStep(null, ref spoken));
			Assert.Equal(0, spoken);
			Assert.Equal("Jack of Bones", MonsterNarration.RosterStep(new[] { "Jack of Bones" }, ref spoken));
		}
	}
}
