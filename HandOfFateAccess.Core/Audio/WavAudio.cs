using System;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Decodes a 16-bit PCM WAV byte buffer into the interleaved float PCM the audio
	/// backend registers. The mod's authored cues (the wall tones, the collision cue)
	/// ship as .wav files on disk; this turns the file bytes into the float buffer the
	/// backend takes, with no Unity dependency, so a botched decode (a silent cue the
	/// blind player can never see is missing) is caught by the offline tests.
	///
	/// It walks the RIFF chunk list rather than assuming a fixed header layout: real
	/// encoders interleave metadata chunks (LIST/INFO, fact) between "fmt " and "data",
	/// and the wall-tone files do exactly that. Only uncompressed PCM at 16 bits is
	/// supported, which is what the shipped clips are; anything else throws with a
	/// clear message rather than producing noise.
	/// </summary>
	public static class WavAudio {
		/// <summary>
		/// Parses <paramref name="bytes"/> into interleaved float samples in -1..1.
		/// Throws <see cref="FormatException"/> on anything not a 16-bit PCM WAV.
		/// </summary>
		public static void Decode(byte[] bytes, out float[] pcm, out int channels, out int sampleRate) {
			if (bytes == null) throw new ArgumentNullException(nameof(bytes));
			if (bytes.Length < 12 || !Tag(bytes, 0, "RIFF") || !Tag(bytes, 8, "WAVE"))
				throw new FormatException("not a RIFF/WAVE file");

			int fmtChannels = 0;
			int fmtSampleRate = 0;
			int fmtBits = 0;
			int fmtFormat = 0;
			int dataOffset = -1;
			int dataLength = 0;

			// Chunks start after the 12-byte RIFF/WAVE preamble. Each is a 4-byte id, a
			// 4-byte little-endian size, then that many bytes, padded to an even boundary.
			int pos = 12;
			while (pos + 8 <= bytes.Length) {
				int size = BitConverter.ToInt32(bytes, pos + 4);
				int body = pos + 8;
				if (size < 0 || body + size > bytes.Length)
					throw new FormatException("chunk size runs past end of file");

				if (Tag(bytes, pos, "fmt ")) {
					// The bounds check above only proves the declared chunk fits; a fmt chunk
					// shorter than the 16 canonical PCM bytes would let the field reads below
					// run into the next chunk or past the buffer. Reject it as malformed here
					// so the caller gets the clear FormatException rather than an index error.
					if (size < 16) throw new FormatException("fmt chunk too small (" + size + " bytes)");
					fmtFormat = BitConverter.ToUInt16(bytes, body);
					fmtChannels = BitConverter.ToUInt16(bytes, body + 2);
					fmtSampleRate = BitConverter.ToInt32(bytes, body + 4);
					fmtBits = BitConverter.ToUInt16(bytes, body + 14);
				} else if (Tag(bytes, pos, "data")) {
					dataOffset = body;
					dataLength = size;
				}

				pos = body + size + (size & 1);
			}

			if (fmtChannels == 0) throw new FormatException("no fmt chunk");
			if (dataOffset < 0) throw new FormatException("no data chunk");
			if (fmtFormat != 1) throw new FormatException("unsupported WAV format " + fmtFormat + " (only uncompressed PCM)");
			if (fmtBits != 16) throw new FormatException("unsupported bit depth " + fmtBits + " (only 16-bit)");

			int sampleCount = dataLength / 2;
			pcm = new float[sampleCount];
			for (int i = 0; i < sampleCount; i++) {
				short s = BitConverter.ToInt16(bytes, dataOffset + i * 2);
				pcm[i] = s / 32768f;
			}
			channels = fmtChannels;
			sampleRate = fmtSampleRate;
		}

		private static bool Tag(byte[] bytes, int offset, string tag) {
			return bytes[offset] == tag[0]
				&& bytes[offset + 1] == tag[1]
				&& bytes[offset + 2] == tag[2]
				&& bytes[offset + 3] == tag[3];
		}
	}
}
