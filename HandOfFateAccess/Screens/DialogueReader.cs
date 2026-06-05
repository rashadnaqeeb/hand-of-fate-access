using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Thin Unity adapter that pulls a live Dialogue menu's raw label text into the
	/// Unity-free DialogueInfo. Does NO formatting -- word choice and the
	/// body/options/fallback policy live in DialogueInfo (Core). The private label
	/// fields are reached by reflection so a game-side rename crashes the build's
	/// audit rather than silently degrading; the UILabels themselves are legitimately
	/// null on dialogue prefabs that omit a button, per the game's own guards.
	/// </summary>
	internal static class DialogueReader {
		private static readonly FieldInfo BodyField = AccessTools.Field(typeof(Dialogue), "m_body");
		private static readonly FieldInfo OkTextField = AccessTools.Field(typeof(Dialogue), "m_optionOKText");
		private static readonly FieldInfo CancelTextField = AccessTools.Field(typeof(Dialogue), "m_optionCancelText");

		public static DialogueInfo Read(Dialogue dialogue) {
			var options = new List<string>();
			AddIfPresent(options, LabelText(OkTextField, dialogue));
			AddIfPresent(options, LabelText(CancelTextField, dialogue));
			return new DialogueInfo(LabelText(BodyField, dialogue), options);
		}

		private static string LabelText(FieldInfo field, Dialogue dialogue) {
			var label = (UILabel)field.GetValue(dialogue);
			return label != null ? label.text : null;
		}

		private static void AddIfPresent(List<string> list, string text) {
			if (!string.IsNullOrEmpty(text))
				list.Add(text);
		}
	}
}
