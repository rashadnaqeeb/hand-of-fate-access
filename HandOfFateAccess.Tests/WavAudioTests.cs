using System;
using System.Collections.Generic;
using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class WavAudioTests {
		// Builds a minimal 16-bit PCM WAV. An optional metadata chunk is inserted
		// between fmt and data, with a deliberately odd size, to prove the decoder walks
		// the chunk list and honours the even-boundary padding rather than assuming data
		// follows fmt (which is how the real wall-tone files are laid out).
		private static byte[] BuildWav(int channels, int sampleRate, short[] samples, bool withMetadata) {
			var data = new List<byte>();
			foreach (short s in samples) data.AddRange(BitConverter.GetBytes(s));

			var fmt = new List<byte>();
			fmt.AddRange(BitConverter.GetBytes((ushort)1));            // PCM
			fmt.AddRange(BitConverter.GetBytes((ushort)channels));
			fmt.AddRange(BitConverter.GetBytes(sampleRate));
			fmt.AddRange(BitConverter.GetBytes(sampleRate * channels * 2)); // byte rate
			fmt.AddRange(BitConverter.GetBytes((ushort)(channels * 2)));    // block align
			fmt.AddRange(BitConverter.GetBytes((ushort)16));               // bits

			var body = new List<byte>();
			AppendChunk(body, "fmt ", fmt.ToArray());
			if (withMetadata) AppendChunk(body, "LIST", new byte[] { 1, 2, 3, 4, 5 }); // odd size
			AppendChunk(body, "data", data.ToArray());

			var file = new List<byte>();
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			file.AddRange(BitConverter.GetBytes(4 + body.Count));
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
			file.AddRange(body);
			return file.ToArray();
		}

		private static void AppendChunk(List<byte> into, string id, byte[] payload) {
			into.AddRange(System.Text.Encoding.ASCII.GetBytes(id));
			into.AddRange(BitConverter.GetBytes(payload.Length));
			into.AddRange(payload);
			if ((payload.Length & 1) == 1) into.Add(0); // pad to even
		}

		[Fact]
		public void DecodesFormatAndSamples() {
			short[] samples = { 0, short.MaxValue, short.MinValue, -16384 };
			byte[] wav = BuildWav(2, 44100, samples, withMetadata: false);

			WavAudio.Decode(wav, out float[] pcm, out int channels, out int sampleRate);

			Assert.Equal(2, channels);
			Assert.Equal(44100, sampleRate);
			Assert.Equal(4, pcm.Length);
			Assert.Equal(0f, pcm[0]);
			Assert.Equal(short.MaxValue / 32768f, pcm[1], 5);
			Assert.Equal(-1f, pcm[2]);          // short.MinValue / 32768 == -1
			Assert.Equal(-0.5f, pcm[3], 5);
		}

		[Fact]
		public void WalksPastMetadataChunkBeforeData() {
			short[] samples = { 100, -100 };
			byte[] wav = BuildWav(1, 22050, samples, withMetadata: true);

			WavAudio.Decode(wav, out float[] pcm, out int channels, out int sampleRate);

			Assert.Equal(1, channels);
			Assert.Equal(22050, sampleRate);
			Assert.Equal(2, pcm.Length);
			Assert.Equal(100 / 32768f, pcm[0], 6);
			Assert.Equal(-100 / 32768f, pcm[1], 6);
		}

		[Fact]
		public void NonRiff_Throws() {
			Assert.Throws<FormatException>(() =>
				WavAudio.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, out _, out _, out _));
		}

		[Fact]
		public void MissingDataChunk_Throws() {
			var body = new List<byte>();
			var fmt = new byte[16];
			fmt[0] = 1; fmt[2] = 1; // format PCM, 1 channel (channels nonzero so fmt is accepted)
			fmt[14] = 16;           // bits
			AppendChunk(body, "fmt ", fmt);
			var file = new List<byte>();
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			file.AddRange(BitConverter.GetBytes(4 + body.Count));
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
			file.AddRange(body);

			Assert.Throws<FormatException>(() => WavAudio.Decode(file.ToArray(), out _, out _, out _));
		}

		[Fact]
		public void ShortFmtChunk_Throws() {
			// A fmt chunk shorter than the canonical 16 bytes must be rejected with a clear
			// FormatException, not an index-out-of-range when its fields are read.
			var body = new List<byte>();
			AppendChunk(body, "fmt ", new byte[8]);            // half a fmt chunk
			AppendChunk(body, "data", new byte[] { 0, 0, 1, 0 });
			var file = new List<byte>();
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			file.AddRange(BitConverter.GetBytes(4 + body.Count));
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
			file.AddRange(body);

			Assert.Throws<FormatException>(() => WavAudio.Decode(file.ToArray(), out _, out _, out _));
		}

		[Fact]
		public void EmptyDataChunk_YieldsEmptyPcm() {
			// A valid header with no samples decodes to an empty buffer rather than throwing.
			byte[] wav = BuildWav(2, 44100, new short[0], withMetadata: false);

			WavAudio.Decode(wav, out float[] pcm, out int channels, out int sampleRate);

			Assert.Empty(pcm);
			Assert.Equal(2, channels);
			Assert.Equal(44100, sampleRate);
		}

		[Fact]
		public void EightBitDepth_Throws() {
			var fmt = new List<byte>();
			fmt.AddRange(BitConverter.GetBytes((ushort)1));
			fmt.AddRange(BitConverter.GetBytes((ushort)1));
			fmt.AddRange(BitConverter.GetBytes(8000));
			fmt.AddRange(BitConverter.GetBytes(8000));
			fmt.AddRange(BitConverter.GetBytes((ushort)1));
			fmt.AddRange(BitConverter.GetBytes((ushort)8)); // 8-bit, unsupported
			var body = new List<byte>();
			AppendChunk(body, "fmt ", fmt.ToArray());
			AppendChunk(body, "data", new byte[] { 0, 1, 2, 3 });
			var file = new List<byte>();
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
			file.AddRange(BitConverter.GetBytes(4 + body.Count));
			file.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
			file.AddRange(body);

			Assert.Throws<FormatException>(() => WavAudio.Decode(file.ToArray(), out _, out _, out _));
		}
	}
}
