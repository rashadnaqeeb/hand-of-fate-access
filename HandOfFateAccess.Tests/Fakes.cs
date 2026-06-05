using System.Collections.Generic;
using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Tests {
	/// <summary>Test clock with manually controlled time.</summary>
	internal sealed class FakeClock : IClock {
		public float Seconds { get; set; }
	}

	/// <summary>Records every Say call so tests can assert on what was spoken.</summary>
	internal sealed class FakeSpeechBackend : ISpeechBackend {
		public readonly List<(string text, bool interrupt)> Spoken = new List<(string, bool)>();
		public int StopCount;
		public int InitCount;
		public int ShutdownCount;

		public bool InitializeResult = true;
		public bool IsInitialized { get; private set; }
		public bool IsAvailable { get; private set; }

		public bool Initialize() {
			InitCount++;
			IsInitialized = true;
			IsAvailable = InitializeResult;
			return InitializeResult;
		}

		public void Shutdown() {
			ShutdownCount++;
			IsInitialized = false;
			IsAvailable = false;
		}

		public void Say(string text, bool interrupt) => Spoken.Add((text, interrupt));

		public void Stop() => StopCount++;
	}
}
