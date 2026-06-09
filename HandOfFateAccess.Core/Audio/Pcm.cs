using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Small PCM clean-up steps applied to rendered speech before it becomes a clip. Pure and
	/// engine-free so they are unit-tested off-engine. SAPI hands back audio that is sometimes
	/// stereo (which Unity's panStereo would place differently than a mono tone at the same pan)
	/// and is bookended by digital silence (which would push the spoken word late behind its
	/// tone); these fold it to mono and tighten its onset.
	/// </summary>
	public static class Pcm {
		/// <summary>Averages a 2-channel interleaved buffer down to mono. Returns the input
		/// unchanged when it is not stereo, so callers can apply it unconditionally.</summary>
		public static float[] DownmixToMono(float[] pcm, int channels) {
			if (pcm == null || channels != 2) return pcm;
			int frames = pcm.Length / 2;
			float[] mono = new float[frames];
			for (int i = 0; i < frames; i++)
				mono[i] = 0.5f * (pcm[2 * i] + pcm[2 * i + 1]);
			return mono;
		}

		/// <summary>
		/// Trims leading and trailing near-silence (|sample| at or below <paramref name="threshold"/>)
		/// from a mono buffer, keeping <paramref name="padFrames"/> of margin so the word's onset
		/// and decay are not clipped. Returns the input unchanged if it is empty, all silence, or
		/// already tight.
		/// </summary>
		public static float[] TrimSilence(float[] pcm, float threshold, int padFrames) {
			if (pcm == null || pcm.Length == 0) return pcm;
			if (padFrames < 0) padFrames = 0;

			int first = -1, last = -1;
			for (int i = 0; i < pcm.Length; i++) {
				if (Math.Abs(pcm[i]) > threshold) {
					if (first < 0) first = i;
					last = i;
				}
			}
			if (first < 0) return pcm;   // all silence; leave for the caller to notice

			int start = first - padFrames;
			if (start < 0) start = 0;
			int end = last + padFrames;
			if (end > pcm.Length - 1) end = pcm.Length - 1;
			if (start == 0 && end == pcm.Length - 1) return pcm;

			int length = end - start + 1;
			float[] trimmed = new float[length];
			Array.Copy(pcm, start, trimmed, 0, length);
			return trimmed;
		}
	}
}
