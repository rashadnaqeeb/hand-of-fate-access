using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The end-of-run reward "add to deck" button: the game's banner (what is being
	/// added) leads, then the authored action word, with an empty banner dropping out.
	/// </summary>
	public class RewardAddElementTests {
		private static string Describe(string banner) =>
			new RewardAddElement(banner).Describe().Resolve();

		[Fact]
		public void Banner_leads_the_action_word() {
			Assert.Equal("New cards unlocked, " + Strings.AddToDeck, Describe("New cards unlocked"));
		}

		[Fact]
		public void Empty_banner_drops_out() {
			Assert.Equal(Strings.AddToDeck, Describe(""));
		}

		[Fact]
		public void Null_banner_drops_out() {
			Assert.Equal(Strings.AddToDeck, Describe(null));
		}
	}
}
