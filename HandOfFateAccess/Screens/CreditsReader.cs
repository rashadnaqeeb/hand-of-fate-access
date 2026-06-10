using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Extracts the credits sections from the live Credits panel: the authored team
	/// lists, then the Kickstarter backer lists. Each list's content is a CreditsData
	/// ScriptableObject (title key plus entries), complete the moment the panel object
	/// is active, so nothing depends on the scrolling labels. Returns null while the
	/// panel is not live yet (the menu enables it a frame or two after the pause state
	/// flips); the caller retries.
	/// </summary>
	internal static class CreditsReader {
		private static readonly FieldInfo ListsField = AccessTools.Field(typeof(Credits), "m_creditsLists");
		private static readonly FieldInfo KsListsField = AccessTools.Field(typeof(Credits), "m_ksCreditsLists");

		public static IList<CreditsSection> Read() {
			Credits credits = UnityEngine.Object.FindObjectOfType<Credits>();
			if (credits == null) return null;
			var sections = new List<CreditsSection>();
			var seen = new HashSet<CreditsData>();
			Add((IEnumerable<CreditsList>)ListsField.GetValue(credits), sections, seen);
			Add((IEnumerable<CreditsList>)KsListsField.GetValue(credits), sections, seen);
			return sections;
		}

		private static void Add(IEnumerable<CreditsList> lists, List<CreditsSection> sections, HashSet<CreditsData> seen) {
			if (lists == null) return;
			foreach (CreditsList list in lists) {
				CreditsData data = list != null ? list.CreditsData : null;
				// A list not yet wired with data, or sharing an asset already read
				// (the Kickstarter window machinery), adds nothing.
				if (data == null || !seen.Add(data)) continue;
				var entries = new List<CreditsEntry>();
				foreach (CreditsData.Entry entry in data.Entries)
					entries.Add(new CreditsEntry(entry.Position, entry.Name));
				// The title is a localization key the game runs through SetLabelText.
				sections.Add(new CreditsSection(UIUtils.GetString(data.Title), entries));
			}
		}
	}
}
