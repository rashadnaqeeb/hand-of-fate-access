using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the dealer's voice-over subtitle line. The dealer narrates throughout play
	/// on a display-only label the focus model never reaches; the screen watcher edge-
	/// polls this and announces each new segment. The game only populates this label when
	/// the player has subtitles enabled, so reading it as-is respects that setting: with
	/// subtitles off the label stays empty and nothing is spoken.
	///
	/// Visibility is reported alongside the text because Hide() does not clear the label:
	/// the last segment lingers after the subtitle closes, so the watcher needs the shown/
	/// hidden state to re-announce a line that plays again. Fields are reached by
	/// reflection so a game-side rename crashes the build's audit rather than degrading
	/// silently.
	/// </summary>
	internal static class SubtitleReader {
		private static readonly FieldInfo LabelField = AccessTools.Field(typeof(Subtitle), "m_label");
		private static readonly FieldInfo TransitionField = AccessTools.Field(typeof(Subtitle), "m_transition");

		/// <summary>
		/// The live subtitle text and whether the subtitle is currently shown. Both default
		/// (null / false) when no subtitle widget is present.
		/// </summary>
		public static void Read(out string text, out bool visible) {
			text = null;
			visible = false;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			Subtitle subtitle = ui.Subtitle;
			if (subtitle == null) return;

			var label = (UILabel)LabelField.GetValue(subtitle);
			text = label != null ? label.text : null;
			var transition = (Transition)TransitionField.GetValue(subtitle);
			visible = transition != null && transition.AutoShow;
		}
	}
}
