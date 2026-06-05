using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;
using HandOfFateAccess.Util;
using HarmonyLib;
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
		private static readonly FieldInfo BlockerGroupField = AccessTools.Field(typeof(UISelection), "m_selectionBlockerGroup");

		// Generic NGUI placeholder names that carry no information. A label-less stop
		// can only ever speak its raw object name; when that name is one of these (the
		// splash-screen click-to-skip area is "Selectable"), the word says nothing, so
		// we suppress it. Meaningful names (Continue, Skip) still read.
		private static readonly HashSet<string> NoiseNames = new HashSet<string> { "Selectable" };

		// Authored readouts for label-less controls whose raw object name reads poorly.
		// The mapping (internal object name -> which authored string) lives here; the
		// spoken word itself lives in Strings (Core), so it stays translatable.
		private static readonly Dictionary<string, string> NameReadouts = new Dictionary<string, string> {
			{ "ContinueButton", Strings.ControlContinue },
		};

		private static readonly string[] EmptyLabels = new string[0];

		public static UIElement Create(GameObject go) {
			UISelectable selectable = go.GetComponent<UISelectable>();
			if (selectable != null && IsBlockerFocus(selectable))
				return null;

			Card card = go.GetComponentInParent<Card>();
			if (card != null)
				return new CardElement(ExtractCard(card));

			// A UISelectableGroup is a structural container in NGUI's selection model,
			// not content. An ordinary group routes focus down to a child selectable
			// (its initial/last selection), whose own Select event follows with the real
			// readout. The group's own object carries no label, so announcing it would
			// speak its bare scene name. Suppress it and let the delegated child speak.
			if (go.GetComponent<UISelectableGroup>() != null)
				return null;

			string[] labels = ExtractLabels(go);
			// When a focused object yields no spoken label text we announce its raw
			// object name as a last resort. Suppress generic placeholder names outright;
			// otherwise log what it actually is (concrete selectable type and owning/
			// blocker group) so the remaining name-only stops can be audited from the log.
			if (new Message().AddRange(labels).Resolve().Length == 0) {
				if (NoiseNames.Contains(go.name)) {
					Log.Debug("focus suppressed for placeholder '" + go.name + "'; " + DescribeSelectable(selectable));
					return null;
				}
				string readout;
				if (NameReadouts.TryGetValue(go.name, out readout))
					return new GenericElement(readout, EmptyLabels);
				Log.Debug("focus fell back to name '" + go.name + "'; " + DescribeSelectable(selectable));
			}
			return new GenericElement(go.name, labels);
		}

		// The blocker group parks focus on a content-less placeholder ("SelectableBlocker")
		// while the game locks input during transitions. Catch it structurally - the
		// focused selectable belongs to its category's blocker group - so it is
		// suppressed whether or not the lock flag (IsBlocked) is currently raised; the
		// flag was only set during some of the placeholder's focus events. The flag is
		// kept as an additional backstop.
		private static bool IsBlockerFocus(UISelectable selectable) {
			UISelection selection = selectable.Selection;
			if (selection == null) return false;
			if (selection.IsBlocked) return true;
			var blocker = (UISelectableGroup)BlockerGroupField.GetValue(selection);
			return blocker != null && selectable.Group == blocker;
		}

		private static string DescribeSelectable(UISelectable selectable) {
			if (selectable == null) return "selectable=none";
			UISelection selection = selectable.Selection;
			UISelectableGroup blocker = selection != null ? (UISelectableGroup)BlockerGroupField.GetValue(selection) : null;
			return "type=" + selectable.GetType().Name
				+ " group=" + (selectable.Group != null ? selectable.Group.name : "none")
				+ " blocker=" + (blocker != null ? blocker.name : "none")
				+ " blocked=" + (selection != null && selection.IsBlocked);
		}

		private static CardInfo ExtractCard(Card card) {
			TokenStake[] tokens = null;
			bool complete = false;
			var encounter = card as EncounterCard;
			if (encounter != null) {
				complete = encounter.Complete;
				tokens = ExtractTokens(encounter);
			}

			// An encounter card's description is the encounter scenario, which is not on
			// the table card (a sighted player sees only art and title) and is read by the
			// event panel when the encounter is played. Reading it on focus too would
			// duplicate it, so omit it here for encounters; the panel is the source.
			string description = encounter != null ? null : card.LocalisedDescription;

			// Card.Title is a raw localization key (e.g. ENCOUNTER_TITLE_TWISTED_CANYON);
			// there is no LocalisedTitle, so localize it here the same way the game's own
			// LocalisedDescription wraps Description. UIUtils.GetString returns the key
			// unchanged if no entry exists, so this is safe for any already-human string.
			return new CardInfo(
				UIUtils.GetString(card.Title),
				description,
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
					titles.Add(UIUtils.GetString(card.Title));
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
