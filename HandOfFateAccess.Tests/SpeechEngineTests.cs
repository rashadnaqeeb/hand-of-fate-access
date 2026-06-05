using HandOfFateAccess.Speech;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class SpeechEngineTests {
		[Fact]
		public void Initialize_DelegatesAndReportsAvailability() {
			var backend = new FakeSpeechBackend { InitializeResult = true };
			bool result = SpeechEngine.Initialize(backend);

			Assert.True(result);
			Assert.Equal(1, backend.InitCount);
			Assert.True(SpeechEngine.IsInitialized);
			Assert.True(SpeechEngine.IsAvailable);
		}

		[Fact]
		public void Initialize_UnavailableBackend_ReportsUnavailable() {
			var backend = new FakeSpeechBackend { InitializeResult = false };
			bool result = SpeechEngine.Initialize(backend);

			Assert.False(result);
			Assert.True(SpeechEngine.IsInitialized);
			Assert.False(SpeechEngine.IsAvailable);
		}

		[Fact]
		public void Say_DelegatesToBackend() {
			var backend = new FakeSpeechBackend();
			SpeechEngine.Initialize(backend);

			SpeechEngine.Say("hello", interrupt: true);

			Assert.Equal(("hello", true), Assert.Single(backend.Spoken));
		}

		[Fact]
		public void StopAndShutdown_DelegateToBackend() {
			var backend = new FakeSpeechBackend();
			SpeechEngine.Initialize(backend);

			SpeechEngine.Stop();
			SpeechEngine.Shutdown();

			Assert.Equal(1, backend.StopCount);
			Assert.Equal(1, backend.ShutdownCount);
		}
	}
}
