using System.Collections.Generic;
using HandOfFateAccess.Audio;
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

	/// <summary>
	/// Records audio calls and hands back real Voice handles from an actual VoicePool,
	/// so feature tests can assert what played and so handle resolution behaves as in
	/// the live backend. No sound is produced.
	/// </summary>
	internal sealed class FakeAudioBackend : IAudioBackend {
		public readonly List<string> Registered = new List<string>();
		public readonly List<(string key, SoundParams p, bool loop)> Played =
			new List<(string, SoundParams, bool)>();
		public readonly List<(Voice voice, SoundParams p)> Updated = new List<(Voice, SoundParams)>();
		public int StopCount;
		public int StopAllCount;

		private readonly VoicePool _pool = new VoicePool(8);

		public bool InitializeResult = true;
		public bool IsInitialized { get; private set; }
		public bool IsAvailable { get; private set; }

		public bool Initialize() {
			IsInitialized = true;
			IsAvailable = InitializeResult;
			return InitializeResult;
		}

		public void Shutdown() {
			IsInitialized = false;
			IsAvailable = false;
		}

		public void Register(string key, float[] pcm, int channels, int sampleRate) =>
			Registered.Add(key);

		public void PlayOneShot(string key, SoundParams parameters) =>
			Play(key, parameters, false);

		public Voice Play(string key, SoundParams parameters, bool loop) {
			Played.Add((key, parameters, loop));
			return _pool.Acquire();
		}

		public void Update(Voice voice, SoundParams parameters) {
			if (_pool.TryResolve(voice, out _))
				Updated.Add((voice, parameters));
		}

		public void Stop(Voice voice) {
			if (_pool.TryResolve(voice, out _)) {
				StopCount++;
				_pool.Release(voice);
			}
		}

		public void StopAll() {
			StopAllCount++;
			for (int slot = 0; slot < _pool.Capacity; slot++)
				_pool.ReleaseSlot(slot);
		}
	}
}
