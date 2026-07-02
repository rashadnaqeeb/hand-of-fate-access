using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// Drives the whole Core engine (registry, voice pool, spatial chain, mix clamp)
	/// through the fake backend, pulling rendered blocks from the hosted mixer exactly
	/// as the FMOD callback would. AudioEngine is a static singleton, so each test
	/// initializes and shuts it down around its body (the suite runs unparallelized).
	/// </summary>
	public class AudioEngineTests : IDisposable {
		private const int Rate = 44100;
		private readonly FakeAudioBackend _backend = new FakeAudioBackend();

		public AudioEngineTests() {
			Assert.True(AudioEngine.Initialize(_backend));
		}

		public void Dispose() => AudioEngine.Shutdown();

		private static float[] Ones(int n) {
			var buffer = new float[n];
			for (int i = 0; i < n; i++) buffer[i] = 1f;
			return buffer;
		}

		private static float Peak(float[] stereo) {
			float peak = 0f;
			for (int i = 0; i < stereo.Length; i++) peak = Math.Max(peak, Math.Abs(stereo[i]));
			return peak;
		}

		[Fact]
		public void Initialize_HostsTheMixer() {
			Assert.NotNull(_backend.Mixer);
			Assert.Equal(0f, Peak(_backend.Render(256)));   // silent with nothing playing
		}

		[Fact]
		public void RegisteredClip_PlaysThroughTheMixer() {
			AudioEngine.Register("test", Ones(500), 1, Rate);
			AudioEngine.PlayOneShot("test", SoundParams.Neutral);
			Assert.True(Peak(_backend.Render(256)) > 0.5f);
		}

		[Fact]
		public void StereoClip_IsFoldedToMono() {
			AudioEngine.Register("stereo", Ones(500), 2, Rate);
			AudioEngine.PlayOneShot("stereo", SoundParams.Neutral);
			Assert.True(Peak(_backend.Render(128)) > 0.5f);
		}

		[Fact]
		public void MultichannelClip_StillPlays() {
			// User-supplied cue WAVs come in any layout; a surround file must fold to
			// mono and play, never be dropped (a dropped telegraph cue is a hit taken).
			AudioEngine.Register("surround", Ones(600), 6, Rate);
			AudioEngine.PlayOneShot("surround", SoundParams.Neutral);
			Assert.True(Peak(_backend.Render(64)) > 0.5f);
		}

		[Fact]
		public void HostFailure_ReportsUnavailable() {
			// A backend that cannot set up its output has no audio: Initialize must
			// fail and IsAvailable must drop, so every feature disables loudly instead
			// of playing a whole session into nothing.
			var broken = new FakeAudioBackend { HostResult = false };
			Assert.False(AudioEngine.Initialize(broken));
			Assert.False(AudioEngine.IsAvailable);
		}

		private sealed class ThrowingSource : IPcmSource {
			public void Fill(float[] buffer, int channels, int frames) => throw new InvalidOperationException("boom");
		}

		private sealed class SteadySource : IPcmSource {
			public void Fill(float[] buffer, int channels, int frames) {
				for (int i = 0; i < frames * channels; i++) buffer[i] = 0.5f;
			}
		}

		[Fact]
		public void FaultingSource_IsDroppedAlone() {
			// One throwing source must lose its own voice, not the whole mix: the rest
			// of the soundscape is everything a blind player navigates by.
			AudioEngine.PlayLive(new SteadySource(), SoundParams.Neutral);
			Voice bad = AudioEngine.PlayLive(new ThrowingSource(), SoundParams.Neutral);

			Assert.True(Peak(_backend.Render(128)) > 0.3f);   // good voice survives the fault
			AudioEngine.Pump();                                // sweeps the killed voice, logs the fault
			Assert.False(AudioEngine.IsPlaying(bad));
			Assert.True(Peak(_backend.Render(128)) > 0.3f);
		}

		[Fact]
		public void UnregisteredKey_ReturnsNone() {
			Assert.False(AudioEngine.Play("missing", SoundParams.Neutral, false).IsValid);
		}

		[Fact]
		public void Voice_LifecycleThroughThePump() {
			AudioEngine.Register("test", Ones(100), 1, Rate);
			Voice voice = AudioEngine.Play("test", SoundParams.Neutral, false);
			Assert.True(voice.IsValid);
			Assert.True(AudioEngine.IsPlaying(voice));

			_backend.Render(512);      // plays out and drains within one block
			AudioEngine.Pump();        // the sweep recycles the finished slot
			Assert.False(AudioEngine.IsPlaying(voice));
		}

		[Fact]
		public void Stop_SilencesALoop() {
			AudioEngine.Register("loop", Ones(100), 1, Rate);
			Voice voice = AudioEngine.Play("loop", SoundParams.Neutral, true);
			Assert.True(Peak(_backend.Render(256)) > 0.5f);

			AudioEngine.Stop(voice);
			_backend.Render(256);      // the fade block
			_backend.Render(256);      // the drain
			AudioEngine.Pump();
			Assert.False(AudioEngine.IsPlaying(voice));
			Assert.Equal(0f, Peak(_backend.Render(256)));
		}

		[Fact]
		public void Update_MovesAPlayingVoice() {
			AudioEngine.Register("loop", Ones(100), 1, Rate);
			Voice voice = AudioEngine.Play("loop", new SoundParams(-1f, 1f, 1f), true);
			float[] left = _backend.Render(256);
			Assert.Equal(0f, RightRms(left), 3);   // hard left: right ear silent

			AudioEngine.Update(voice, new SoundParams(1f, 1f, 1f));
			_backend.Render(256);                  // the ramp block
			float[] right = _backend.Render(256);
			Assert.True(RightRms(right) > 0.9f);   // now hard right
		}

		[Fact]
		public void PlayBuffer_PlaysACallerHeldBuffer() {
			Voice voice = AudioEngine.PlayBuffer(Ones(300), Rate, false, SoundParams.Neutral);
			Assert.True(voice.IsValid);
			Assert.True(Peak(_backend.Render(128)) > 0.5f);
		}

		[Fact]
		public void StopAll_SilencesEverything() {
			AudioEngine.Register("loop", Ones(100), 1, Rate);
			AudioEngine.Play("loop", SoundParams.Neutral, true);
			AudioEngine.Play("loop", new SoundParams(1f, 1f, 0.5f), true);
			Assert.True(Peak(_backend.Render(128)) > 0f);

			AudioEngine.StopAll();
			_backend.Render(256);
			_backend.Render(256);
			Assert.Equal(0f, Peak(_backend.Render(128)));
		}

		[Fact]
		public void FinishedSlots_RecycleForever() {
			// Far more one-shots than the pool holds: with the pump sweeping finished
			// voices, none may ever be dropped.
			AudioEngine.Register("blip", Ones(50), 1, Rate);
			for (int i = 0; i < 200; i++) {
				Voice voice = AudioEngine.Play("blip", SoundParams.Neutral, false);
				Assert.True(voice.IsValid, $"pool exhausted at one-shot {i}");
				_backend.Render(256);
				AudioEngine.Pump();
			}
		}

		[Fact]
		public void ManyVoices_SumWithoutClippingPastFullScale() {
			AudioEngine.Register("loud", Ones(2000), 1, Rate);
			for (int i = 0; i < 10; i++)
				AudioEngine.Play("loud", SoundParams.Neutral, true);
			// Ten centered full-volume voices sum way past 1; the mixer clamps.
			Assert.True(Peak(_backend.Render(256)) <= 1f);
		}

		private static float RightRms(float[] stereo) {
			double sum = 0;
			int frames = stereo.Length / 2;
			for (int f = 0; f < frames; f++) sum += stereo[f * 2 + 1] * stereo[f * 2 + 1];
			return (float)Math.Sqrt(sum / frames);
		}
	}
}
