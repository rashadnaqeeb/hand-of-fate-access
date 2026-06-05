using System.Collections.Generic;
using HandOfFateAccess.Speech;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class SpeechPipelineTests {
		private readonly FakeClock _clock;
		private readonly List<(string text, bool interrupt)> _spoken;

		public SpeechPipelineTests() {
			_clock = new FakeClock();
			_spoken = new List<(string, bool)>();
			SpeechPipeline.Reset();
			SpeechPipeline.Clock = _clock;
			SpeechPipeline.SpeakAction = (t, i) => _spoken.Add((t, i));
		}

		[Fact]
		public void Disabled_SuppressesAllSpeech() {
			SpeechPipeline.SetEnabled(false);
			SpeechPipeline.SpeakInterrupt("hi");
			SpeechPipeline.SpeakQueued("hi");
			Assert.Empty(_spoken);
		}

		[Fact]
		public void Interrupt_SpeaksWithInterruptFlag() {
			SpeechPipeline.SpeakInterrupt("hi");
			Assert.Equal(("hi", true), Assert.Single(_spoken));
		}

		[Fact]
		public void Queued_SpeaksWithoutInterruptFlag() {
			SpeechPipeline.SpeakQueued("hi");
			Assert.Equal(("hi", false), Assert.Single(_spoken));
		}

		[Fact]
		public void Speak_FiltersMarkup() {
			SpeechPipeline.SpeakInterrupt("[b]hi[/b]");
			Assert.Equal(("hi", true), Assert.Single(_spoken));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("[b][/b]")]
		public void Speak_EmptyAfterFilter_NotSpoken(string input) {
			SpeechPipeline.SpeakInterrupt(input);
			SpeechPipeline.SpeakQueued(input);
			Assert.Empty(_spoken);
		}

		[Fact]
		public void Interrupt_DedupsSameTextWithinWindow() {
			_clock.Seconds = 0f;
			SpeechPipeline.SpeakInterrupt("hi");
			_clock.Seconds = 0.01f; // inside the 0.05s window
			SpeechPipeline.SpeakInterrupt("hi");
			Assert.Single(_spoken);

			_clock.Seconds = 0.07f; // past the window
			SpeechPipeline.SpeakInterrupt("hi");
			Assert.Equal(2, _spoken.Count);
		}

		[Fact]
		public void Interrupt_DifferentTextNotDeduped() {
			_clock.Seconds = 0f;
			SpeechPipeline.SpeakInterrupt("hi");
			_clock.Seconds = 0.01f;
			SpeechPipeline.SpeakInterrupt("bye");
			Assert.Equal(2, _spoken.Count);
		}

		[Fact]
		public void Queued_NotDeduped() {
			_clock.Seconds = 0f;
			SpeechPipeline.SpeakQueued("hi");
			SpeechPipeline.SpeakQueued("hi");
			Assert.Equal(2, _spoken.Count);
		}
	}
}
