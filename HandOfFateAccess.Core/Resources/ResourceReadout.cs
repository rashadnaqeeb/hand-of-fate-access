using HandOfFateAccess.UI;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// A plain snapshot of the player's current resources, extracted by the plugin's
	/// ResourceReader from the live model. Each Has* flag mirrors whether the game shows
	/// that resource to a sighted player (it puts a stat card for it on the table), so a
	/// resource the run does not use (commonly iron ore) is left out rather than read as
	/// zero. Tokens have no stat card; they are shown only when collected, so a count of
	/// zero means none to read.
	/// </summary>
	public sealed class ResourceSnapshot {
		public bool HasHealth;
		public int Health;
		public int MaxHealth;
		public bool HasFood;
		public int Food;
		public bool HasGold;
		public int Gold;
		public bool HasIronOre;
		public int IronOre;
		public int Tokens;
	}

	/// <summary>
	/// Composes the on-demand resource readout from a snapshot: each visible resource as
	/// "value noun", in order of relevance (health first as the survival stat, then food,
	/// gold, iron ore, tokens). Health reads as current of max ("200/200 health"). Built
	/// here in Core so the wording and ordering are unit-tested; the plugin only supplies
	/// the raw numbers and visibility. Returns "" when nothing is visible (no live run).
	/// </summary>
	public static class ResourceReadout {
		public static string Compose(ResourceSnapshot s) {
			if (s == null) return "";
			var message = new Message();
			if (s.HasHealth)
				message.Add(s.Health + "/" + s.MaxHealth + " " + ResourceText.Noun(ResourceKind.Health));
			if (s.HasFood)
				message.Add(s.Food + " " + ResourceText.Noun(ResourceKind.Food));
			if (s.HasGold)
				message.Add(s.Gold + " " + ResourceText.Noun(ResourceKind.Gold));
			if (s.HasIronOre)
				message.Add(s.IronOre + " " + ResourceText.Noun(ResourceKind.IronOre));
			if (s.Tokens > 0)
				message.Add(s.Tokens + " " + ResourceText.Noun(ResourceKind.Tokens));
			return message.Resolve();
		}
	}
}
