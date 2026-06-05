using HandOfFateAccess.Localization;

namespace HandOfFateAccess.Resources {
	/// <summary>The run resources the mod tracks, in rough order of relevance.</summary>
	public enum ResourceKind {
		Health,
		MaxHealth,
		Food,
		Gold,
		IronOre,
		Tokens,
	}

	/// <summary>
	/// Maps a resource to its spoken noun and formats a live change ("-5 health", "+3
	/// gold"). The noun is mod-authored (the game's STAT_ keys are format strings, not
	/// bare nouns); the sign and amount come from the change the game reports.
	/// </summary>
	public static class ResourceText {
		public static string Noun(ResourceKind kind) {
			switch (kind) {
				case ResourceKind.Health: return Strings.ResourceHealth;
				case ResourceKind.MaxHealth: return Strings.ResourceMaxHealth;
				case ResourceKind.Food: return Strings.ResourceFood;
				case ResourceKind.Gold: return Strings.ResourceGold;
				case ResourceKind.IronOre: return Strings.ResourceIronOre;
				case ResourceKind.Tokens: return Strings.ResourceTokens;
				default: return "";
			}
		}

		/// <summary>
		/// A change line: the signed amount then the noun, e.g. "-5 health", "+3 gold".
		/// A zero change has no news and returns "" (callers should not announce it).
		/// </summary>
		public static string Delta(ResourceKind kind, int diff) {
			if (diff == 0) return "";
			string sign = diff > 0 ? "+" : "";
			return sign + diff + " " + Noun(kind);
		}
	}
}
