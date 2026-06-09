using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class PcmTests {
		[Fact]
		public void Downmix_AveragesStereoToMono() {
			float[] stereo = { 1f, 0f, 0.4f, 0.6f, -1f, 1f };   // 3 frames
			float[] mono = Pcm.DownmixToMono(stereo, 2);
			Assert.Equal(new[] { 0.5f, 0.5f, 0f }, mono);
		}

		[Fact]
		public void Downmix_PassesMonoThrough() {
			float[] mono = { 0.1f, 0.2f, 0.3f };
			Assert.Same(mono, Pcm.DownmixToMono(mono, 1));
		}

		[Fact]
		public void Trim_RemovesLeadingAndTrailingSilenceWithPad() {
			// 4 silent, 3 audible, 5 silent; threshold 0.01, pad 2 frames.
			float[] buf = { 0f, 0f, 0f, 0f, 0.5f, 0.9f, 0.5f, 0f, 0f, 0f, 0f, 0f };
			float[] trimmed = Pcm.TrimSilence(buf, 0.01f, 2);
			// first audible at 4, last at 6; pad 2 -> keep [2..8] = 7 frames.
			Assert.Equal(7, trimmed.Length);
			Assert.Equal(0f, trimmed[0]);          // padding frame (index 2)
			Assert.Equal(0.5f, trimmed[2]);        // the onset (original index 4)
			Assert.Equal(0.9f, trimmed[3]);
		}

		[Fact]
		public void Trim_AllSilenceReturnsInput() {
			float[] buf = { 0f, 0.001f, -0.002f, 0f };
			Assert.Same(buf, Pcm.TrimSilence(buf, 0.01f, 2));
		}

		[Fact]
		public void Trim_AlreadyTightReturnsInput() {
			float[] buf = { 0.5f, -0.5f, 0.5f };
			Assert.Same(buf, Pcm.TrimSilence(buf, 0.01f, 4));
		}

		[Fact]
		public void Trim_DoesNotClipOnset_WithZeroPad() {
			float[] buf = { 0f, 0f, 0.3f, 0.7f, 0.3f, 0f };
			float[] trimmed = Pcm.TrimSilence(buf, 0.01f, 0);
			Assert.Equal(new[] { 0.3f, 0.7f, 0.3f }, trimmed);
		}
	}
}
