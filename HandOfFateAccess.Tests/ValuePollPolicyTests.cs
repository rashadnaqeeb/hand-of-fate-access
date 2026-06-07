using HandOfFateAccess.Focus;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class ValuePollPolicyTests {
		[Fact]
		public void StatCard_PolledRegardlessOfInput() {
			Assert.True(ValuePollPolicy.ShouldPoll(isStat: true, wasRecentInput: false));
			Assert.True(ValuePollPolicy.ShouldPoll(isStat: true, wasRecentInput: true));
		}

		[Fact]
		public void Selector_PolledOnlyAfterInput() {
			// A selector/toggle changes only from the user's own input; without recent
			// input it must not be read, so a label that animates on its own is not
			// announced as if the user changed it.
			Assert.False(ValuePollPolicy.ShouldPoll(isStat: false, wasRecentInput: false));
			Assert.True(ValuePollPolicy.ShouldPoll(isStat: false, wasRecentInput: true));
		}

		[Fact]
		public void AutomaticStatChange_Queues() {
			// A stat card moving on a game event with no input queues so it does not cut
			// off whatever is speaking.
			Assert.Equal(SpeechMode.Queued, ValuePollPolicy.Delivery(isStat: true, wasRecentInput: false));
		}

		[Fact]
		public void StatChangeFromInput_Interrupts() {
			// The user acted on the stat card (e.g. spending), so the change is a direct
			// response and interrupts.
			Assert.Equal(SpeechMode.Interrupt, ValuePollPolicy.Delivery(isStat: true, wasRecentInput: true));
		}

		[Fact]
		public void SelectorChange_Interrupts() {
			// A selector change is always the direct response to the user's left/right.
			Assert.Equal(SpeechMode.Interrupt, ValuePollPolicy.Delivery(isStat: false, wasRecentInput: true));
		}
	}
}
