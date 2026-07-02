using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// The one output the backend hosts: sums every live <see cref="SpatialVoice"/>
	/// into interleaved stereo on the audio thread. Idle voices cost a state check;
	/// the summed block is clamped once here, the single place many voices can add up
	/// past full scale. Emits silence when nothing plays, so the hosting channel runs
	/// for the whole session and starting a sound is just activating a voice.
	///
	/// Faults are isolated per voice: a voice (or its live source) that throws is
	/// killed and the rest of the mix keeps sounding - one faulty projectile must
	/// never silence the whole combat soundscape. The fault message is parked for
	/// the engine's pump to log (this runs on the audio thread, which cannot log).
	/// </summary>
	public sealed class AudioMixer : IPcmSource {
		// Blocks are rendered in chunks so voice scratch stays bounded (and under
		// SpatialVoice.MaxBlockFrames, its live-source contract).
		private const int ChunkFrames = 1024;

		private readonly SpatialVoice[] _voices;
		private readonly float[] _stereo = new float[ChunkFrames * 2];
		private volatile string _fault;

		public AudioMixer(SpatialVoice[] voices) {
			_voices = voices;
		}

		public void Fill(float[] buffer, int channels, int frames) {
			int done = 0;
			while (done < frames) {
				int n = frames - done;
				if (n > ChunkFrames) n = ChunkFrames;

				Array.Clear(_stereo, 0, n * 2);
				for (int v = 0; v < _voices.Length; v++) {
					try {
						_voices[v].MixInto(_stereo, n);
					} catch (Exception ex) {
						_voices[v].Kill();
						if (_fault == null) _fault = ex.Message;
					}
				}

				Write(buffer, done * channels, channels, n);
				done += n;
			}
		}

		/// <summary>The last parked voice fault, cleared on read; null when none.
		/// Read once per frame by the engine's pump, which does the logging.</summary>
		public string TakeFault() {
			string fault = _fault;
			_fault = null;
			return fault;
		}

		// Clamp and adapt the stereo chunk to the requested channel count: stereo
		// verbatim, mono averaged, a wider layout gets the pair up front and silence
		// on the rest.
		private void Write(float[] buffer, int offset, int channels, int frames) {
			for (int f = 0; f < frames; f++) {
				float l = Clamp(_stereo[f * 2]);
				float r = Clamp(_stereo[f * 2 + 1]);
				int at = offset + f * channels;
				if (channels == 1) {
					buffer[at] = 0.5f * (l + r);
				} else {
					buffer[at] = l;
					buffer[at + 1] = r;
					for (int c = 2; c < channels; c++) buffer[at + c] = 0f;
				}
			}
		}

		private static float Clamp(float v) => v > 1f ? 1f : (v < -1f ? -1f : v);
	}
}
