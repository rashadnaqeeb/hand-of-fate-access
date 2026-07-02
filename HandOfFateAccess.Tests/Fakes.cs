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
	/// A backend that opens no device: it hands the hosted mixer back to the test,
	/// which pulls rendered blocks from it directly. That drives the WHOLE Core
	/// engine (registry, voice pool, spatial chain, mix clamp) exactly as the FMOD
	/// callback would, so tests assert on the actual samples a player would hear.
	/// </summary>
	internal sealed class FakeAudioBackend : IAudioBackend {
		public IPcmSource Mixer;
		public int PumpCount;

		public bool InitializeResult = true;
		public bool HostResult = true;
		public bool IsInitialized { get; private set; }
		public bool IsAvailable { get; private set; }
		public int OutputSampleRate => IsAvailable ? 44100 : 0;
		public bool IsOutputMono => false;

		public bool Initialize() {
			IsInitialized = true;
			IsAvailable = InitializeResult;
			return InitializeResult;
		}

		public void Shutdown() {
			IsInitialized = false;
			IsAvailable = false;
			Mixer = null;
		}

		public void Pump() => PumpCount++;

		public bool Host(IPcmSource mixer) {
			if (!HostResult) {
				IsAvailable = false;   // matches the real backend: no output = unavailable
				return false;
			}
			Mixer = mixer;
			return true;
		}

		/// <summary>Pull one interleaved-stereo block of <paramref name="frames"/> frames
		/// from the hosted mixer, as the audio thread would.</summary>
		public float[] Render(int frames) {
			var buffer = new float[frames * 2];
			Mixer.Fill(buffer, 2, frames);
			return buffer;
		}
	}
}
