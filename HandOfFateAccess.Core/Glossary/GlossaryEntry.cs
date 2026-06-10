using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// One playback step of a glossary entry's demo: which clip, at what voice
	/// parameters, and for how long. A looping step (a clip authored as a continuous
	/// loop, like the wall tones) is stopped after <see cref="HoldSeconds"/>; a
	/// one-shot step plays itself out, and HoldSeconds is only the gap before the
	/// NEXT step starts (zero for a final step).
	/// </summary>
	public readonly struct GlossaryStep {
		public readonly string ClipKey;
		public readonly SoundParams Params;
		public readonly bool Loop;
		public readonly float HoldSeconds;

		public GlossaryStep(string clipKey, SoundParams parameters, bool loop, float holdSeconds) {
			ClipKey = clipKey;
			Params = parameters;
			Loop = loop;
			HoldSeconds = holdSeconds;
		}
	}

	/// <summary>
	/// One sound in the glossary: the line spoken as the player reaches it in the
	/// list (the sound's name, then what it means), and the demo steps played when
	/// they activate it. Most entries are a single step; the wall tones play their
	/// four sides in sequence.
	/// </summary>
	public sealed class GlossaryEntry {
		public readonly string Label;
		public readonly GlossaryStep[] Steps;

		public GlossaryEntry(string label, GlossaryStep[] steps) {
			Label = label;
			Steps = steps;
		}
	}
}
