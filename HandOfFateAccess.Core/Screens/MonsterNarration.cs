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
	/// one card's phrase; RosterStep announces the combat line-up the dealer deals onto the
	/// table, one card at a time as each lands.
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
		/// Titles joined into one spoken line ("Queen of Skeletons, 3 of Ratmen"), in deal
		/// order. Each title is filtered for speech and empties drop out. Duplicates are
		/// kept (two identical groups are two real enemies), so this does not use the
		/// Message deduper. Returns "" when nothing is staged.
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

		/// <summary>
		/// One polling step of the combat line-up announcement. The dealer deals the cards
		/// one at a time with each flight animation awaited before the next, so announcing
		/// the recomposed roster on every change would speak each partial line-up and then
		/// re-include it in the next ("2 of Dust" then "2 of Dust, 2 of Skulls"), which by
		/// ear is indistinguishable from a real duplicate card. Instead each card is spoken
		/// once, as it is dealt: spokenCount is how many leading titles have already been
		/// announced; growth returns the unspoken tail (joined by Roster) and advances the
		/// marker. A shrink (cards leaving for the arena) or a null read (no live container)
		/// clamps the marker silently so the next encounter's deal announces afresh.
		/// </summary>
		public static string RosterStep(IList<string> titles, ref int spokenCount) {
			int count = titles == null ? 0 : titles.Count;
			if (count <= spokenCount) {
				spokenCount = count;
				return string.Empty;
			}
			var fresh = new List<string>();
			for (int i = spokenCount; i < count; i++)
				fresh.Add(titles[i]);
			spokenCount = count;
			return Roster(fresh);
		}
	}
}
