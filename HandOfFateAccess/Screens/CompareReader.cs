using System.Reflection;
using HandOfFateAccess.Screens;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the equipment-replace comparison: the new item and the equipped item it
	/// would replace, shown side by side on two display-only InfoPanels the focus model
	/// never reaches. The prompt is only this replace decision when BOTH panels are shown
	/// (the new panel alone is reused for plain card info during a normal zoom, which the
	/// focus path already covers), so the watcher reads it only then. Reached from the
	/// live UIManager.ComparePanelManager singleton; private fields are reached by
	/// reflection so a game-side rename crashes the build's audit rather than degrading
	/// silently. Values are read live; nothing is cached.
	/// </summary>
	internal static class CompareReader {
		private static readonly FieldInfo NewPanelField = AccessTools.Field(typeof(ComparePanelManager), "m_new");
		private static readonly FieldInfo OldPanelField = AccessTools.Field(typeof(ComparePanelManager), "m_old");
		private static readonly FieldInfo TitleField = AccessTools.Field(typeof(InfoPanel), "m_title");
		private static readonly FieldInfo StatTitleField = AccessTools.Field(typeof(InfoPanel), "m_statTitle");
		private static readonly FieldInfo StatValueField = AccessTools.Field(typeof(InfoPanel), "m_statValue");
		private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(InfoPanel), "m_description");

		/// <summary>
		/// The new and equipped items when the replace comparison is showing (both panels
		/// visible); both null otherwise.
		/// </summary>
		public static void Read(out CompareItem newItem, out CompareItem oldItem) {
			newItem = null;
			oldItem = null;
			UIManager ui = UIManager.Instance;
			if (ui == null) return;
			ComparePanelManager compare = ui.ComparePanelManager;
			if (compare == null) return;

			var newPanel = (InfoPanel)NewPanelField.GetValue(compare);
			var oldPanel = (InfoPanel)OldPanelField.GetValue(compare);
			if (!Shown(newPanel) || !Shown(oldPanel)) return;

			newItem = Extract(newPanel);
			oldItem = Extract(oldPanel);
		}

		private static bool Shown(InfoPanel panel) {
			return panel != null && panel.Transition != null && panel.Transition.AutoShow;
		}

		private static CompareItem Extract(InfoPanel panel) {
			return new CompareItem {
				Title = LabelText(TitleField, panel),
				StatTitle = LabelText(StatTitleField, panel),
				StatValue = LabelText(StatValueField, panel),
				Description = LabelText(DescriptionField, panel),
			};
		}

		private static string LabelText(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}
	}
}
