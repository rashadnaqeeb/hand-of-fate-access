using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Glossary;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class GlossaryCatalogTests {
		[Fact]
		public void Entries_AllHaveLabelAndAtLeastOneStep() {
			foreach (var entry in GlossaryCatalog.Entries) {
				Assert.False(string.IsNullOrEmpty(entry.Label));
				Assert.NotEmpty(entry.Steps);
			}
		}

		[Fact]
		public void Entries_LabelsAreUnique() {
			var seen = new HashSet<string>();
			foreach (var entry in GlossaryCatalog.Entries)
				Assert.True(seen.Add(entry.Label), "duplicate label: " + entry.Label);
		}

		[Fact]
		public void Steps_AllHaveClipKeyAndAudibleParams() {
			foreach (var entry in GlossaryCatalog.Entries)
				foreach (var step in entry.Steps) {
					Assert.False(string.IsNullOrEmpty(step.ClipKey));
					Assert.True(step.Params.Volume > 0f, entry.Label + ": silent demo step");
					Assert.Equal(1f, step.Params.Pitch);
				}
		}

		[Fact]
		public void Steps_EveryLoopHasAHoldToStopOn() {
			// A looping step with no hold would play forever; a non-final one-shot with
			// no hold would fire its successor in the same frame, stacking the sounds.
			foreach (var entry in GlossaryCatalog.Entries)
				for (int i = 0; i < entry.Steps.Length; i++) {
					var step = entry.Steps[i];
					if (step.Loop || i + 1 < entry.Steps.Length)
						Assert.True(step.HoldSeconds > 0f, entry.Label + ": step " + i + " has no hold");
				}
		}

		[Fact]
		public void WallTonesEntry_PlaysAllFourSidesAtTheirPans() {
			// The looping multi-step entry: four side tones in sequence, the side walls
			// hard-panned to their ears, fore and aft centered, like the live feature.
			GlossaryEntry walls = null;
			foreach (var entry in GlossaryCatalog.Entries)
				if (entry.Steps.Length > 1 && entry.Steps[0].Loop)
					walls = entry;
			Assert.NotNull(walls);
			Assert.Equal(4, walls.Steps.Length);
			Assert.Equal(1f, walls.Steps[0].Params.Pan);
			Assert.Equal(-1f, walls.Steps[1].Params.Pan);
			Assert.Equal(0f, walls.Steps[2].Params.Pan);
			Assert.Equal(0f, walls.Steps[3].Params.Pan);
			foreach (var step in walls.Steps)
				Assert.True(step.Loop);
		}

		[Fact]
		public void RechargeEntry_PlaysWeaponLeftThenArtifactRight() {
			// The pan is the slot identity, so the demo must play both sides, in the
			// order the spoken label names them: weapon hard left, artifact hard right.
			GlossaryEntry recharge = null;
			foreach (var entry in GlossaryCatalog.Entries)
				foreach (var step in entry.Steps)
					if (step.ClipKey == RechargeCueComposer.Key)
						recharge = entry;
			Assert.NotNull(recharge);
			Assert.Equal(2, recharge.Steps.Length);
			Assert.Equal(-1f, recharge.Steps[0].Params.Pan);
			Assert.Equal(1f, recharge.Steps[1].Params.Pan);
			foreach (var step in recharge.Steps)
				Assert.False(step.Loop);
		}

		[Fact]
		public void ProjectileRender_FillsTheRequestedSecondAudibly() {
			float[] samples = ProjectileSynth.Render(44100, GlossaryCatalog.ProjectileDemoSeconds, false, 1u);
			Assert.Equal(44100, samples.Length);
			float peak = 0f;
			foreach (float s in samples)
				if (System.Math.Abs(s) > peak) peak = System.Math.Abs(s);
			Assert.True(peak > 0.01f, "demo render is silent");
		}

		[Fact]
		public void ProjectileRender_ReflectedTumblesDifferently() {
			// Same seed, so the noise matches; only the tempo scale separates them.
			float[] normal = ProjectileSynth.Render(44100, 1f, false, 1u);
			float[] reflected = ProjectileSynth.Render(44100, 1f, true, 1u);
			bool differs = false;
			for (int i = 0; i < normal.Length && !differs; i++)
				differs = normal[i] != reflected[i];
			Assert.True(differs);
		}
	}
}
