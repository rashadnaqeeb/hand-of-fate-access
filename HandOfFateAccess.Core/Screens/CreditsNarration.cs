using System.Collections.Generic;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// A credits section as extracted by the adapter: the localized section title and
	/// its entries in display order. Position is the role line shown above a run of
	/// names ("Design"); most entries carry only a name.
	/// </summary>
	public sealed class CreditsSection {
		public readonly string Title;
		public readonly IList<CreditsEntry> Entries;

		public CreditsSection(string title, IList<CreditsEntry> entries) {
			Title = title;
			Entries = entries ?? new List<CreditsEntry>();
		}
	}

	public struct CreditsEntry {
		public readonly string Position;
		public readonly string Name;

		public CreditsEntry(string position, string name) {
			Position = position;
			Name = name;
		}
	}

	/// <summary>
	/// Composes one spoken line per credits section: the title, then each role and
	/// name in order. The credits are display-only scrolling text the focus model
	/// never reaches, so this is the sole reader. Composed by plain join, not
	/// Message: Message collapses duplicate parts, and a repeated role or name in
	/// the credits is real content, not an NGUI shadow copy.
	/// </summary>
	public static class CreditsNarration {
		public static string ComposeSection(CreditsSection section) {
			var parts = new List<string>();
			if (!string.IsNullOrEmpty(section.Title))
				parts.Add(section.Title);
			foreach (CreditsEntry entry in section.Entries) {
				if (!string.IsNullOrEmpty(entry.Position))
					parts.Add(entry.Position);
				if (!string.IsNullOrEmpty(entry.Name))
					parts.Add(entry.Name);
			}
			return string.Join(", ", parts.ToArray());
		}
	}
}
