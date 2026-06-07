using System.Collections.Generic;
using HandOfFateAccess.Speech;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes spoken text for monster cards. A monster card's face is a playing-card
	/// header: a rank (the creature count for a number card, or Jack/Queen/King for a
	/// face card), the connective "of", and the creature name ("3 of Ratmen", "Queen of
	/// Skeletons"). The game splits that across separate labels, and its own LocalisedTitle
	/// property bakes in the icon count (wrong for a face card, which then reads "1 of ..."),
	/// so the pieces are extracted raw by the adapter and joined here instead. Title builds
	/// one card's phrase; Roster joins the combat line-up the dealer deals onto the table
	/// into a single announcement.
	/// </summary>
	public static class MonsterNarration {
		/// <summary>
		/// One monster card's title phrase, e.g. "3 of Ratmen" or "Queen of Skeletons".
		/// Space-joined (it is a single phrase, not a list) with empty pieces dropped. The
		/// pieces are the game's own localized strings; this only orders and joins them, and
		/// returns the phrase raw for the Message/TextFilter pipeline downstream.
		/// </summary>
		public static string Title(string rank, string connector, string creature) {
			var parts = new List<string>();
			if (!string.IsNullOrEmpty(rank)) parts.Add(rank);
			if (!string.IsNullOrEmpty(connector)) parts.Add(connector);
			if (!string.IsNullOrEmpty(creature)) parts.Add(creature);
			return string.Join(" ", parts.ToArray());
		}

		/// <summary>
		/// The combat line-up joined into one spoken line ("Queen of Skeletons, 3 of
		/// Ratmen"), in deal order. Each title is filtered for speech and empties drop out.
		/// Duplicates are kept (two identical groups are two real enemies), so this does not
		/// use the Message deduper. Returns "" when nothing is staged.
		/// </summary>
		public static string Roster(IList<string> titles) {
			if (titles == null) return string.Empty;
			var kept = new List<string>();
			foreach (string title in titles) {
				string filtered = TextFilter.FilterForSpeech(title);
				if (!string.IsNullOrEmpty(filtered))
					kept.Add(filtered);
			}
			return string.Join(", ", kept.ToArray());
		}
	}
}
