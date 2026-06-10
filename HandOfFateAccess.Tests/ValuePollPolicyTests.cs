using HandOfFateAccess.Focus;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ValuePollPolicyTests {
		[Fact]
		public void AlwaysPolledControl_PolledRegardlessOfInput() {
			Assert.True(ValuePollPolicy.ShouldPoll(alwaysPoll: true, wasRecentInput: false));
			Assert.True(ValuePollPolicy.ShouldPoll(alwaysPoll: true, wasRecentInput: true));
		}

		[Fact]
		public void OtherControl_PolledOnlyAfterInput() {
			// A control with no separable value changes only from the user's own input;
			// without recent input it must not be read, so a label that animates on its
			// own is not announced as if the user changed it.
			Assert.False(ValuePollPolicy.ShouldPoll(alwaysPoll: false, wasRecentInput: false));
			Assert.True(ValuePollPolicy.ShouldPoll(alwaysPoll: false, wasRecentInput: true));
		}

		[Fact]
		public void AutomaticChange_Queues() {
			// A stat card moving on a game event, or a display switch completing frames
			// after the input window closed, queues so it does not cut off whatever is
			// speaking.
			Assert.Equal(SpeechMode.Queued, ValuePollPolicy.Delivery(alwaysPoll: true, wasRecentInput: false));
		}

		[Fact]
		public void ChangeFromInput_Interrupts() {
			// The user acted (spending on a stat card, left/right on a settings row), so
			// the change is a direct response and interrupts.
			Assert.Equal(SpeechMode.Interrupt, ValuePollPolicy.Delivery(alwaysPoll: true, wasRecentInput: true));
		}

		[Fact]
		public void NonPolledControlChange_Interrupts() {
			// Reachable only just after input, which makes it a direct response.
			Assert.Equal(SpeechMode.Interrupt, ValuePollPolicy.Delivery(alwaysPoll: false, wasRecentInput: true));
		}
	}
}
