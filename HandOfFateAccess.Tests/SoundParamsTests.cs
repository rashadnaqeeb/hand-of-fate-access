using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class SoundParamsTests {
		[Fact]
		public void Neutral_IsCenteredUnmodifiedFullVolume() {
			var n = SoundParams.Neutral;
			Assert.Equal(0f, n.Pan);
			Assert.Equal(1f, n.Pitch);
			Assert.Equal(1f, n.Volume);
		}

		[Fact]
		public void Clamped_InRangeValues_Unchanged() {
			var p = new SoundParams(0.5f, 1.5f, 0.75f).Clamped();
			Assert.Equal(0.5f, p.Pan);
			Assert.Equal(1.5f, p.Pitch);
			Assert.Equal(0.75f, p.Volume);
		}

		[Fact]
		public void Clamped_PanBeyondField_ClampsToUnit() {
			Assert.Equal(-1f, new SoundParams(-4f, 1f, 1f).Clamped().Pan);
			Assert.Equal(1f, new SoundParams(4f, 1f, 1f).Clamped().Pan);
		}

		[Fact]
		public void Clamped_VolumeOutOfRange_ClampsToZeroOne() {
			Assert.Equal(0f, new SoundParams(0f, 1f, -0.5f).Clamped().Volume);
			Assert.Equal(1f, new SoundParams(0f, 1f, 2f).Clamped().Volume);
		}

		[Fact]
		public void Clamped_PitchOutOfRange_ClampsToTwoOctaves() {
			Assert.Equal(SoundParams.MinPitch, new SoundParams(0f, 0.01f, 1f).Clamped().Pitch);
			Assert.Equal(SoundParams.MaxPitch, new SoundParams(0f, 99f, 1f).Clamped().Pitch);
		}

		[Fact]
		public void Clamped_DefaultStruct_PitchFlooredNotSilent() {
			// default(SoundParams) has Pitch 0; Clamped floors it so a misuse degrades
			// audibly to MinPitch rather than into a stuck, silent voice.
			var p = default(SoundParams).Clamped();
			Assert.Equal(SoundParams.MinPitch, p.Pitch);
		}
	}
}
