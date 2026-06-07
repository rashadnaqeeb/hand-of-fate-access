using System.Collections.Generic;
using HandOfFateAccess.Focus;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the combat line-up the dealer deals onto the table for an encounter: the
	/// monster cards staged in the live EncounterContainer, in deal order. These cards are
	/// never focusable (at level-in they animate straight off the table into the 3D arena
	/// as spawners), so the focus model never reaches them; the screen watcher edge-polls
	/// this and announces the roster while they sit on the table. Each card's title is
	/// composed by the shared ProxyFactory path so it matches the choice-list readout.
	/// Null when no deck/container is live; an empty list when no monsters are staged.
	/// </summary>
	internal static class MonsterRosterReader {
		public static IList<string> Read() {
			DeckManager deck = DeckManager.Instance;
			EncounterContainer container = deck != null ? deck.EncounterContainer : null;
			if (container == null) return null;
			var titles = new List<string>();
			foreach (Card card in container.Cards) {
				var monster = card as MonsterCard;
				if (monster != null)
					titles.Add(ProxyFactory.MonsterTitle(monster));
			}
			return titles;
		}
	}
}
