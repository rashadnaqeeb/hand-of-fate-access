using System;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// The per-sample mixing for a gambit voice: reads a mono source buffer at an arbitrary rate
	/// (linear-interpolated, so a 22 kHz spoken word and a 48 kHz tone share one path) and writes
	/// it into an interleaved output block with equal-power panning. Equal-power (cos/sin), not
	/// Unity's panStereo, so the gambit matches the validated prototype and the rest of the mod's
	/// spatial audio: a centred voice is -3 dB and positions read evenly across the field.
	///
	/// Pure and engine-free so the audio-thread logic is unit-tested off-engine; the plugin's
	/// GambitVoice calls it from the backend's synth fill callback.
	/// </summary>
	public static class GambitVoiceDsp {
		/// <summary>
		/// Fills <paramref name="frames"/> frames of <paramref name="output"/> from mono
		/// <paramref name="buffer"/> starting at <paramref name="pos"/>, advancing
		/// <paramref name="step"/> source samples per output frame, panned to <paramref name="pan"/>
		/// at <paramref name="volume"/>. Loops when <paramref name="loop"/>; otherwise stops at the
		/// end, zero-filling the rest and setting <paramref name="finished"/>. <paramref name="output"/>
		/// must be at least frames times channels long. Returns the new read position.
		/// </summary>
		public static double Fill(float[] buffer, double pos, double step, bool loop,
				float pan, float volume, float[] output, int channels, int frames, out bool finished) {
			finished = false;
			int len = buffer != null ? buffer.Length : 0;
			// A non-positive or non-finite step (a bad source rate) would never advance, or would
			// drive pos negative into an out-of-range read on the audio thread. Refuse it rather
			// than crash the audio callback silently.
			if (len == 0 || channels < 1 || !(step > 0.0)) {
				Array.Clear(output, 0, frames * channels);
				finished = true;
				return pos;
			}

			float clampedPan = pan < -1f ? -1f : (pan > 1f ? 1f : pan);
			float angle = (clampedPan + 1f) * 0.25f * (float)Math.PI;   // 0..pi/2 across left..right
			float leftGain = (float)Math.Cos(angle) * volume;
			float rightGain = (float)Math.Sin(angle) * volume;

			int j = 0;
			for (int i = 0; i < frames; i++) {
				float s = 0f;
				if (!finished) {
					if (pos >= len) {
						if (loop) pos -= len * Math.Floor(pos / len);
						else finished = true;
					}
					if (!finished) {
						int i0 = (int)pos;
						double frac = pos - i0;
						float a = buffer[i0];
						float b = i0 + 1 < len ? buffer[i0 + 1] : (loop ? buffer[0] : buffer[i0]);
						s = (float)(a + (b - a) * frac);
						if (float.IsNaN(s)) s = 0f;
						else if (s > 1f) s = 1f;
						else if (s < -1f) s = -1f;
						pos += step;
					}
				}

				if (channels >= 2) {
					output[j] = s * leftGain;
					output[j + 1] = s * rightGain;
					for (int c = 2; c < channels; c++) output[j + c] = 0f;
				} else {
					output[j] = s * volume;
				}
				j += channels;
			}
			return pos;
		}
	}
}
