using HandOfFateAccess.Speech;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// The status-key handler: read the player's current resources live and speak the
	/// whole line. The adapter (ResourceReader) extracts the raw values, Core composes
	/// the wording (ResourceReadout), and this speaks it, interrupting since it is an
	/// on-demand lookup the player asked for and so supersedes pending speech. Off a
	/// run the readout is the "no run" line, so the key is never silent.
	/// </summary>
	internal static class ResourceStatus {
		public static void Speak() {
			SpeechPipeline.SpeakInterrupt(ResourceReadout.ComposeStatus(ResourceReader.Read()));
		}
	}
}
