using HandOfFateAccess.Focus;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class FocusAnnouncePolicyTests {
		[Fact]
		public void UserDrivenWithNoFreshContext_Interrupts() {
			Assert.Equal(SpeechMode.Interrupt, FocusAnnouncePolicy.Decide(userInitiated: true, screenJustChanged: false));
		}

		[Fact]
		public void GameDrivenWithNoFreshContext_Queues() {
			Assert.Equal(SpeechMode.Queued, FocusAnnouncePolicy.Decide(userInitiated: false, screenJustChanged: false));
		}

		[Fact]
		public void FreshContext_QueuesEvenWhenUserDriven() {
			// Pressing confirm to open a dialogue: the dialogue text leads, the confirm
			// button the game lands focus on queues behind it rather than cutting it off.
			Assert.Equal(SpeechMode.Queued, FocusAnnouncePolicy.Decide(userInitiated: true, screenJustChanged: true));
		}

		[Fact]
		public void FreshContext_QueuesWhenGameDriven() {
			Assert.Equal(SpeechMode.Queued, FocusAnnouncePolicy.Decide(userInitiated: false, screenJustChanged: true));
		}
	}
}
