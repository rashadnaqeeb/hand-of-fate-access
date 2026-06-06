using System.Collections.Generic;
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

			s.HasHealth = IsVisible(ResourceKind.Health);
			s.HasFood = IsVisible(ResourceKind.Food);
			s.HasGold = IsVisible(ResourceKind.Gold);
			s.HasIronOre = IsVisible(ResourceKind.IronOre);

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

		/// <summary>
		/// Whether the game is currently showing this resource to a sighted player, i.e.
		/// its stat card is on the table. The stat hand is dealt when a run begins and
		/// drained when it ends, so this is false in menus and during the launch/run-start
		/// setup where Player.Reset writes the starting values, and true throughout a live
		/// run. During combat the cards leave the hand for its combat container
		/// (Hand.SendToCombatContainer), so both are checked: the resource is shown in
		/// either, which is exactly when the game pops its floating amount. MaxHealth
		/// shares the health card.
		/// </summary>
		public static bool IsVisible(ResourceKind kind) {
			DeckManager deck = DeckManager.Instance;
			StatHand hand = deck != null ? deck.PlayerStatHand : null;
			if (hand == null) return false;
			return HasStatCard(hand.Cards, kind)
				|| HasStatCard(hand.CombatContainer.Cards, kind);
		}

		private static bool HasStatCard(List<Card> cards, ResourceKind kind) {
			if (cards == null) return false;
			foreach (Card c in cards) {
				switch (kind) {
					case ResourceKind.Health:
					case ResourceKind.MaxHealth:
						if (c is HealthStatCard) return true;
						break;
					case ResourceKind.Food:
						if (c is FoodStatCard) return true;
						break;
					case ResourceKind.Gold:
						if (c is GoldStatCard) return true;
						break;
					case ResourceKind.IronOre:
						if (c is IronOreStatCard) return true;
						break;
				}
			}
			return false;
		}
	}
}
