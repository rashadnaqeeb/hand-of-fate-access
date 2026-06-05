using System.Collections.Generic;
using HandOfFateAccess.UI;
using UnityEngine;

namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Thin Unity adapter and single dispatch point: turns a focused GameObject into
	/// the Core UIElement that knows how to describe it. Does NO formatting - it only
	/// pulls raw values off live components into Unity-free Core types; word choice
	/// lives in the elements (Core). A later ScreenManager can wrap this to override
	/// element selection per screen.
	///
	/// A Card's UISelectableItem (the focus target) sits on the Card's own
	/// GameObject, so the focused object usually is the card itself;
	/// GetComponentInParent resolves the owning Card robustly whether focus lands on
	/// it directly or on a nested selectable. When a Card is found we read its data
	/// model (complete info) and never also sweep its labels, which would
	/// double-speak. Everything else falls back to the generic label sweep.
	///
	/// Returns null for a focused object that is not content (see below); the caller
	/// treats null as "nothing to announce" and skips it.
	/// </summary>
	internal static class ProxyFactory {
		public static UIElement Create(GameObject go) {
			Card card = go.GetComponentInParent<Card>();
			if (card != null)
				return new CardElement(ExtractCard(card));

			// A UISelectableGroup is a structural container in NGUI's selection model,
			// not content. An ordinary group routes focus down to a child selectable
			// (its initial/last selection), whose own Select event follows with the real
			// readout; the selection blocker group is an input lock used during
			// transitions and loading. Either way the group's own object carries no
			// label, so announcing it would speak its bare scene name ("Selectable",
			// "SelectableBlocker"). Suppress it and let the delegated child speak.
			if (go.GetComponent<UISelectableGroup>() != null)
				return null;

			return new GenericElement(go.name, ExtractLabels(go));
		}

		private static CardInfo ExtractCard(Card card) {
			TokenStake[] tokens = null;
			bool complete = false;
			var encounter = card as EncounterCard;
			if (encounter != null) {
				complete = encounter.Complete;
				tokens = ExtractTokens(encounter);
			}

			return new CardInfo(
				card.Title,
				card.LocalisedDescription,
				card.StatValueString,
				card.ValueString,
				tokens,
				complete);
		}

		// Extract the cards each token grants/removes as raw titles only; the
		// "gain"/"lose" wording is applied in Core (CardElement).
		private static TokenStake[] ExtractTokens(EncounterCard encounter) {
			Token[] prefabs = encounter.TokenPrefabs;
			if (prefabs == null || prefabs.Length == 0)
				return null;

			var stakes = new List<TokenStake>();
			foreach (Token token in prefabs) {
				if (token == null) continue;
				string gain = CardTitles(token.CardSetToAdd);
				string remove = CardTitles(token.CardSetToRemove);
				if (gain.Length > 0 || remove.Length > 0)
					stakes.Add(new TokenStake(gain, remove));
			}
			return stakes.ToArray();
		}

		private static string CardTitles(CardSet set) {
			if (set == null) return "";
			List<Card> cards = set.Cards;
			if (cards == null || cards.Count == 0) return "";

			var titles = new List<string>();
			foreach (Card card in cards)
				if (card != null && !string.IsNullOrEmpty(card.Title))
					titles.Add(card.Title);
			return string.Join(", ", titles.ToArray());
		}

		private static string[] ExtractLabels(GameObject go) {
			UILabel[] labels = go.GetComponentsInChildren<UILabel>();
			var texts = new string[labels.Length];
			for (int i = 0; i < labels.Length; i++)
				texts[i] = labels[i].text;
			return texts;
		}
	}
}
