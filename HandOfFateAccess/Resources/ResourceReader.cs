using HandOfFateAccess.Resources;
using UnityEngine;

namespace HandOfFateAccess.Resources {
	/// <summary>
	/// Extracts the player's current resources into a Unity-free snapshot for the
	/// on-demand readout. Visibility mirrors the sighted view: a resource is included
	/// only when the game shows its stat card on the table (read from the player's stat
	/// hand), so a run that does not use iron ore leaves it out rather than reading zero.
	/// Tokens have no stat card and are reported by count, shown only when collected.
	/// Values are read live at call time; nothing is cached.
	///
	/// This is the data half of the resource readout. The trigger (a key) is not wired
	/// yet: the mod has no custom input layer, so Read stands ready for that to call.
	/// </summary>
	internal static class ResourceReader {
		public static ResourceSnapshot Read() {
			var s = new ResourceSnapshot();
			Player p = Player.Instance;
			if (p == null) return s;

			DeckManager deck = DeckManager.Instance;
			StatHand hand = deck != null ? deck.PlayerStatHand : null;
			if (hand != null && hand.Cards != null) {
				foreach (Card c in hand.Cards) {
					if (c is HealthStatCard) s.HasHealth = true;
					else if (c is FoodStatCard) s.HasFood = true;
					else if (c is GoldStatCard) s.HasGold = true;
					else if (c is IronOreStatCard) s.HasIronOre = true;
				}
			}

			if (s.HasHealth) {
				s.Health = Mathf.CeilToInt(p.Health);
				s.MaxHealth = Mathf.CeilToInt(p.MaxHealth);
			}
			if (s.HasFood) s.Food = p.Food.BaseValue;
			if (s.HasGold) s.Gold = p.Gold.BaseValue;
			if (s.HasIronOre) s.IronOre = p.IronOre.BaseValue;
			s.Tokens = p.Tokens.Count;
			return s;
		}
	}
}
