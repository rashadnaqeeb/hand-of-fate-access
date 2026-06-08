using System.Text;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Names an end-of-run reward token for speech. A token has no localized name and no
	/// on-card label, only a gem icon and an internal id like "Token_WhiteMinotaur5(Clone)",
	/// so a sighted player recognises it by its art. The gem art is the granting encounter's
	/// token sprite, so the localized title of the card that grants the token (resolved by the
	/// adapter) is the matching identity, and Compose speaks it verbatim: that title already
	/// carries the tier number, so nothing is appended. When no granter is found, Name
	/// synthesises a name from the id alone as a fallback: the "Token_" prefix and "(Clone)"
	/// suffix are stripped and the id is split at camel-case and letter/digit boundaries
	/// (keeping its digits). That fallback is English-shaped and not translatable, the
	/// deliberate exception for a label-less prop with no localized source.
	/// </summary>
	public static class TokenNarration {
		/// <summary>
		/// The spoken token name: the granting card's localized title (which already carries
		/// the tier number), or the id-synthesised name when no title was resolved.
		/// </summary>
		public static string Compose(string grantingTitle, string rawId) {
			return string.IsNullOrEmpty(grantingTitle) ? Name(rawId) : grantingTitle;
		}

		public static string Name(string rawId) {
			if (string.IsNullOrEmpty(rawId)) return string.Empty;
			string id = rawId.Replace("(Clone)", "");
			if (id.StartsWith("Token_")) id = id.Substring("Token_".Length);

			var sb = new StringBuilder(id.Length + 8);
			for (int i = 0; i < id.Length; i++) {
				char c = id[i];
				// Any separator (underscore, space, punctuation) collapses to a single space.
				if (!char.IsLetterOrDigit(c)) {
					if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
					continue;
				}
				if (i > 0 && WordBoundary(id, i) && sb.Length > 0 && sb[sb.Length - 1] != ' ')
					sb.Append(' ');
				sb.Append(c);
			}
			return sb.ToString().Trim();
		}

		// A new spoken word starts at char i when: an uppercase follows a lowercase or digit
		// ("White|Minotaur", "5|Boss" via the digit rule below); a digit follows a letter or a
		// letter follows a digit ("Minotaur|5", "5|Boss"); or an uppercase ends an acronym run
		// by being followed by a lowercase ("XML|Token").
		private static bool WordBoundary(string s, int i) {
			char c = s[i];
			char prev = s[i - 1];
			if (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev))) return true;
			if (char.IsDigit(c) && char.IsLetter(prev)) return true;
			if (char.IsLetter(c) && char.IsDigit(prev)) return true;
			if (char.IsUpper(c) && char.IsUpper(prev) && i + 1 < s.Length && char.IsLower(s[i + 1])) return true;
			return false;
		}
	}
}
