using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the dealer's voice-over subtitle line. The dealer narrates throughout play
	/// on a display-only label the focus model never reaches; the screen watcher edge-
	/// polls this and announces each new segment. The game only populates this label when
	/// the player has subtitles enabled, so reading it as-is respects that setting: with
	/// subtitles off the label stays empty and nothing is spoken. The label field is
	/// reached by reflection so a game-side rename crashes the build's audit rather than
	/// degrading silently.
	/// </summary>
	internal static class SubtitleReader {
		private static readonly FieldInfo LabelField = AccessTools.Field(typeof(Subtitle), "m_label");

		/// <summary>The live subtitle text, or null when no subtitle widget is present.</summary>
		public static string Read() {
			UIManager ui = UIManager.Instance;
			if (ui == null) return null;
			Subtitle subtitle = ui.Subtitle;
			if (subtitle == null) return null;
			var label = (UILabel)LabelField.GetValue(subtitle);
			return label != null ? label.text : null;
		}
	}
}
