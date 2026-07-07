using System;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// Renders blocks through the spatial chain (as the audio thread would) and asserts
	/// on the samples: this is the placement the player actually hears.
	/// </summary>
	public class SpatialVoiceTests {
		private const int Rate = 44100;

		private static float[] Render(SpatialVoice voice, int frames) {
			var stereo = new float[frames * 2];
			voice.MixInto(stereo, frames);
			return stereo;
		}

		private static float[] Ones(int n) {
			var buffer = new float[n];
			for (int i = 0; i < n; i++) buffer[i] = 1f;
			return buffer;
		}

		private static float Rms(float[] stereo, int channel) {
			double sum = 0;
			int frames = stereo.Length / 2;
			for (int f = 0; f < frames; f++) sum += stereo[f * 2 + channel] * stereo[f * 2 + channel];
			return (float)Math.Sqrt(sum / frames);
		}

		// --- Lifecycle: a one-shot plays, drains its delay tail, and reports finished. ---

		[Fact]
		public void OneShot_PlaysThenFinishes() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(100), Rate, false, SoundParams.Neutral);
			Assert.False(voice.IsIdle);

			float[] block = Render(voice, 256);
			Assert.True(Math.Abs(block[0]) > 0.5f);       // sounding from the first frame
			Assert.True(voice.IsFinished);                 // 100 + tail fits inside one block
			Assert.Equal(0f, block[255 * 2]);              // silent after the drain

			voice.Recycle();
			Assert.True(voice.IsIdle);
		}

		[Fact]
		public void EmptyClip_FinishesImmediately() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(new float[0], Rate, false, SoundParams.Neutral);
			Assert.True(voice.IsFinished);
		}

		[Fact]
		public void Loop_KeepsSounding() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(50), Rate, true, SoundParams.Neutral);
			float[] block = Render(voice, 500);
			for (int f = 0; f < 500; f++)
				Assert.True(Math.Abs(block[f * 2]) > 0.5f);
			Assert.False(voice.IsFinished);
		}

		[Fact]
		public void Stop_FadesThenFinishes() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(50), Rate, true, SoundParams.Neutral);
			Render(voice, 256);
			voice.RequestStop();
			float[] fade = Render(voice, 256);
			// The stop block ramps to silence: it ends at least 10x quieter than it starts.
			Assert.True(Math.Abs(fade[255 * 2]) < Math.Abs(fade[0]) * 0.1f);
			Render(voice, 256);   // drain the tail
			Assert.True(voice.IsFinished);
		}

		// --- Placement: pan and ITD, read off the rendered samples. ---

		[Fact]
		public void HardLeft_RightEarSilent() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(200), Rate, false, new SoundParams(-1f, 1f, 1f));
			float[] block = Render(voice, 128);
			Assert.True(Rms(block, 0) > 0.9f);
			Assert.Equal(0f, Rms(block, 1), 3);
		}

		[Fact]
		public void Centered_EqualPowerBothEars() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(200), Rate, false, SoundParams.Neutral);
			float[] block = Render(voice, 128);
			Assert.Equal(0.7071f, Rms(block, 0), 2);
			Assert.Equal(0.7071f, Rms(block, 1), 2);
		}

		[Fact]
		public void Itd_DelaysTheFarEar() {
			// An impulse from half-right: the right (near) ear hears it at frame 0, the
			// left (far) ear about half the max interaural delay later.
			var impulse = new float[200];
			impulse[0] = 1f;
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(impulse, Rate, false, new SoundParams(0.5f, 1f, 1f));
			float[] block = Render(voice, 128);

			int firstRight = FirstAudibleFrame(block, 1);
			int firstLeft = FirstAudibleFrame(block, 0);
			int expected = (int)(Spatializer.MaxItdSeconds * Rate * 0.5f);
			Assert.Equal(0, firstRight);
			Assert.True(firstLeft >= expected - 1 && firstLeft <= expected + 1,
				$"far ear arrived at {firstLeft}, expected ~{expected}");
		}

		[Fact]
		public void Chain_AddsNoColoration() {
			// No filter anywhere in the chain: the output is the input times the gains
			// alone, at every bearing.
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(200), Rate, false, new SoundParams(0f, 1f, 1f));
			float[] block = Render(voice, 128);
			for (int f = 1; f < 128; f++)
				Assert.Equal(block[2], block[f * 2], 4);   // constant, no ring/smear
		}

		// --- Live updates ramp instead of stepping, so a moving source never clicks. ---

		[Fact]
		public void Update_RampsAcrossTheBlock() {
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(4000), Rate, true, SoundParams.Neutral);
			Render(voice, 256);

			voice.SetParams(new SoundParams(0f, 1f, 0f));   // volume to zero
			float[] fade = Render(voice, 256);
			// Ends near silent, but no sample-to-sample step bigger than the ramp slope.
			Assert.True(Math.Abs(fade[255 * 2]) < 0.02f);
			float maxStep = 0f;
			for (int f = 1; f < 256; f++)
				maxStep = Math.Max(maxStep, Math.Abs(fade[f * 2] - fade[(f - 1) * 2]));
			Assert.True(maxStep < 0.01f, $"clicked: step {maxStep}");
		}

		[Fact]
		public void NaNUpdate_DoesNotPoisonTheVoice() {
			// One frame of degenerate game geometry (a NaN offset) must not silence a
			// long-lived loop forever: the params gate neutralizes it and the voice
			// keeps sounding, then follows the next finite update.
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(50), Rate, true, SoundParams.Neutral);
			Render(voice, 256);

			voice.SetParams(new SoundParams(float.NaN, float.NaN, float.NaN));
			float[] block = Render(voice, 256);
			for (int i = 0; i < block.Length; i++)
				Assert.False(float.IsNaN(block[i]));

			voice.SetParams(SoundParams.Neutral);
			Render(voice, 256);   // ramp back
			Assert.True(Rms(Render(voice, 256), 0) > 0.5f);
		}

		[Fact]
		public void Pitch_ChangesTheReadRate() {
			// At pitch 2 a 100-sample one-shot is over in ~50 output frames.
			var voice = new SpatialVoice(Rate);
			voice.PlayClip(Ones(100), Rate, false, new SoundParams(0f, 2f, 1f));
			float[] block = Render(voice, 256);
			int audible = 0;
			for (int f = 0; f < 256; f++)
				if (Math.Abs(block[f * 2]) > 0.1f) audible++;
			Assert.InRange(audible, 45, 55);
		}

		// --- A live mono source is pulled through the same chain, resampled at the
		// pitch step so the placement law's darker-means-behind covers synths too. ---

		private sealed class ConstantSource : IPcmSource {
			public void Fill(float[] buffer, int channels, int frames) {
				for (int i = 0; i < frames * channels; i++) buffer[i] = 0.5f;
			}
		}

		private sealed class RampSource : IPcmSource {
			private int _n;
			public int LastRequested;
			public void Fill(float[] buffer, int channels, int frames) {
				LastRequested = frames;
				for (int i = 0; i < frames * channels; i++) buffer[i] = _n++ * 0.001f;
			}
		}

		[Fact]
		public void LiveSource_IsPulledAndPlaced() {
			var voice = new SpatialVoice(Rate);
			voice.PlayLive(new ConstantSource(), new SoundParams(1f, 1f, 1f));
			float[] block = Render(voice, 128);
			Assert.Equal(0f, Rms(block, 0), 3);       // hard right: left silent
			Assert.Equal(0.5f, Rms(block, 1), 2);
			voice.RequestStop();
			Render(voice, 256);
			Render(voice, 256);
			Assert.True(voice.IsFinished);
		}

		[Fact]
		public void LiveSource_PitchedDown_ConsumesFewerSourceSamples() {
			// A behind placement pitches the voice down; the live pull must slow to
			// match (half the pitch, half the source samples per block), or the synth's
			// rhythm would run ahead of what the player hears.
			var source = new RampSource();
			var voice = new SpatialVoice(Rate);
			voice.PlayLive(source, new SoundParams(0f, 0.5f, 1f));
			Render(voice, 128);
			Assert.Equal(64, source.LastRequested);
		}

		[Fact]
		public void LiveSource_ResamplesSeamlesslyAcrossBlocks() {
			// The pitched-down read interpolates the pulled stream: on a linear ramp the
			// rendered output is itself a smooth ramp, with no jump at a block boundary.
			var voice = new SpatialVoice(Rate);
			voice.PlayLive(new RampSource(), new SoundParams(0f, 0.75f, 1f));
			float[] first = Render(voice, 128);
			float[] second = Render(voice, 128);

			float previous = first[0];
			for (int f = 1; f < 128; f++) {
				AssertRampStep(previous, first[f * 2]);
				previous = first[f * 2];
			}
			for (int f = 0; f < 128; f++) {
				AssertRampStep(previous, second[f * 2]);
				previous = second[f * 2];
			}
		}

		private static void AssertRampStep(float previous, float current) {
			// One output step on a 0.001-per-sample ramp at pitch 0.75, times the
			// centered equal-power gain: monotonic (within float rounding) and never a
			// discontinuity.
			float step = current - previous;
			Assert.InRange(step, -1e-6f, 0.001f);
		}

		private static int FirstAudibleFrame(float[] stereo, int channel) {
			int frames = stereo.Length / 2;
			for (int f = 0; f < frames; f++)
				if (Math.Abs(stereo[f * 2 + channel]) > 1e-3f) return f;
			return -1;
		}
	}
}
