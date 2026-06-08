namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Resolves the localized name of an end-of-run reward token by finding the card that
	/// grants it. A token carries only an id, no name, and the token-to-card link is stored
	/// one way (each encounter lists its TokenPrefabs), so this scans the encounter and court
	/// (boss) card pools for the granter and returns its localized title. The gem art a
	/// sighted player sees on the token is that encounter's own token sprite, so its title is
	/// the matching identity. Returns null when no granter is found (a DLC or special token),
	/// so the caller falls back to a name synthesised from the id. Raw extraction only; the
	/// composition (title plus tier, or the fallback) lives in Core.
	/// </summary>
	internal static class TokenReader {
		public static string GrantingTitle(Token token) {
			string id = token.Id;
			if (string.IsNullOrEmpty(id)) return null;

			// Walk the plain card array, not CardManager.EncounterCards: that property rebuilds
			// a filtered dictionary on every access (LINQ Where + ToDictionary), and this runs
			// on a focus event. Filter to encounter cards inline instead.
			CardManager cards = CardManager.Instance;
			if (cards != null && cards.Cards != null) {
				foreach (Card card in cards.Cards) {
					var encounter = card as EncounterCard;
					if (encounter != null && Grants(encounter, id))
						return UIUtils.GetString(encounter.Title);
				}
			}

			CourtCardManager court = CourtCardManager.Instance;
			if (court != null && court.CourtCardPrefabs != null) {
				foreach (Card card in court.CourtCardPrefabs) {
					var encounter = card as EncounterCard;
					if (encounter != null && Grants(encounter, id))
						return UIUtils.GetString(encounter.Title);
				}
			}
			return null;
		}

		// An encounter grants the token if it lists it directly, or if one of its sequence
		// sub-encounters does (a token can be awarded by a later stage of a sequence, the same
		// case the game handles in EncounterCard.RefreshTokenSpriteInternal).
		private static bool Grants(EncounterCard encounter, string id) {
			if (HasToken(encounter, id)) return true;
			Encounter prefab = encounter.EncounterPrefab;
			if (prefab != null && prefab.SequenceEncounterCards != null)
				foreach (EncounterCard sequence in prefab.SequenceEncounterCards)
					if (sequence != null && HasToken(sequence, id))
						return true;
			return false;
		}

		private static bool HasToken(EncounterCard encounter, string id) {
			Token[] tokens = encounter.TokenPrefabs;
			if (tokens == null) return false;
			foreach (Token token in tokens)
				if (token != null && token.Id == id)
					return true;
			return false;
		}
	}
}
