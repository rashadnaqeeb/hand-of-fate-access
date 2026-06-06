using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Screens;
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
		private static readonly FieldInfo ChoiceTextField = AccessTools.Field(typeof(UIChoiceButton), "m_choiceText");
		private static readonly FieldInfo ChoiceLetterField = AccessTools.Field(typeof(UIChoiceButton), "m_letterText");
		// The card's "new" and "pinned" badges and its token gem are unlabelled sprites the
		// game sets only where it draws them, so reading their live state matches the visual
		// per context instead of re-deriving from cross-screen data. The gem in particular
		// also lights for a token granted by a later stage of a sequenced encounter, which a
		// per-card CanGiveTokens check would miss; the sprite already accounts for it.
		private static readonly FieldInfo CardNewBadgeField = AccessTools.Field(typeof(CardTemplate), "m_new");
		private static readonly FieldInfo CardPinnedBadgeField = AccessTools.Field(typeof(CardTemplate), "m_pinned");
		private static readonly FieldInfo CardTokenSpriteField = AccessTools.Field(typeof(CardTemplate), "m_tokenSprite");

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

			// A card being zoomed for a decision (examine, equip, buy, keep...) is force-
			// selected and locked as the sole selection, often re-selecting the card the
			// player just had focused (deduped) or while it is mid deal-animation (skipped),
			// so the focus path cannot reliably read it; and the zoom's lore/compare panels
			// are display-only. The ZoomReader owns the whole zoom instead, so suppress focus
			// here to avoid double-speaking the card when focus does land.
			if (go.GetComponentInParent<ZoomContainer>() != null)
				return null;

			// A map slot is a card stack, not a single card: an event can deal extra cards
			// under the encounter, and the generic sweep below would read every one as a
			// jumble. Route the whole slot through MapSlotReader, which reads the top card
			// plus any attached cards structurally. Checked before the Card branch so a map
			// card resolves to its slot whether focus lands on the slot or the card. An
			// empty slot yields null here and falls through to generic handling.
			MapLayoutSlot mapSlot = go.GetComponentInParent<MapLayoutSlot>();
			if (mapSlot != null) {
				MapSlotInfo slotInfo = MapSlotReader.Read(mapSlot);
				if (slotInfo != null)
					return new MapSlotElement(slotInfo);
			}

			Card card = go.GetComponentInParent<Card>();
			if (card != null)
				return new CardElement(ExtractCard(card));

			// A cabinet card is focused through its CardContainer's selectable, which shares
			// the container's GameObject while the card sits as a child, so the Card branch
			// above (GetComponentInParent) never reaches it and the generic sweep below would
			// read the raw title label, missing completion and leaking face-down identities.
			// Read the container's top card through the card model instead. An empty cabinet
			// slot has no top card and falls through to generic handling.
			CabinetContainer cabinet = go.GetComponentInParent<CabinetContainer>();
			if (cabinet != null && cabinet.TopCard != null)
				return new CardElement(ExtractCard(cabinet.TopCard));

			// In the deck builder's select-mode screen the player moves across the deck
			// piles (Archetype, Equipment, Encounter). Each pile is focused through its
			// DeckBuilderMode's CardContainer selectable, which shares the container's
			// GameObject while the deck's cards sit as children, so the Card branch above
			// never reaches it and the generic sweep below would read the top card's title
			// (e.g. "Monk", "Twisted Canyon") as if it were the pile's label. Announce the
			// mode's own title instead (mode.TitleText is a localization key; UIUtils.GetString
			// localizes it), plus the card count and ready state the game shows on the pile.
			DeckBuilderMode mode = go.GetComponentInParent<DeckBuilderMode>();
			if (mode != null && mode.Container != null && mode.Container.gameObject == go) {
				// The container identity above is the reliable gate, so commit to the pile
				// readout here rather than ever falling back to the generic sweep, which would
				// read the top card's title as the pile name (the bug this block fixes). Only
				// the limited piles (equipment, encounter) carry a count toward a required
				// limit; the Archetype pile is a single pick and reads title only.
				string title = UIUtils.GetString(mode.TitleText);
				var limited = mode as DeckBuilderLimitedMode;
				DeckPileInfo info = limited != null
					? new DeckPileInfo(title, limited.CardCount, limited.CardLimit)
					: new DeckPileInfo(title);
				return new DeckPileElement(info);
			}

			// An equipment slot in the paperdoll is a CardContainer carrying only sprites,
			// no label. A filled slot forwards focus to its card (handled by the Card branch
			// above); an empty one keeps focus on itself and would otherwise fall through to
			// its bare object name ("PaperdollSlot(Clone)"). Announce the slot's category from
			// the game's own localized title, plus that it is empty. GetCategoryTitle returns
			// a localization key; UIUtils.GetString localizes it (and returns "" for none).
			PaperdollSlot slot = go.GetComponent<PaperdollSlot>();
			if (slot != null && slot.TopCard == null) {
				string category = UIUtils.GetString(InventoryCategoryData.GetCategoryTitle(slot.CategoryData.type));
				return new EquipmentSlotElement(category);
			}

			// An encounter choice button focuses a UISelectableItem that is a separate
			// object from its labels, so the generic child sweep misses them. Resolve the
			// owning UIChoiceButton and extract its raw labels; ordering and cleanup are
			// decided in Core (ChoiceElement). The choice text already carries the success
			// odds, which the game bakes into the label ("... (75% chance of success)").
			UIChoiceButton choice = go.GetComponentInParent<UIChoiceButton>();
			if (choice != null) {
				string text = LabelText(ChoiceTextField, choice);
				if (!string.IsNullOrEmpty(text))
					return new ChoiceElement(new ChoiceInfo(LabelText(ChoiceLetterField, choice), text));
			}

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
			// Check the blocker group first. The game's IsBlocked is LockedGroup ==
			// m_selectionBlockerGroup; if a category has no blocker group wired, that is
			// null == null when nothing is locked, i.e. IsBlocked reads true always, and
			// keying off it would silently drop every focus on the category. So suppress
			// only when a real blocker exists: the category is locked to it, or the
			// focused object belongs to it.
			var blocker = (UISelectableGroup)BlockerGroupField.GetValue(selection);
			if (blocker == null) return false;
			return selection.IsBlocked || selectable.Group == blocker;
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

		// Shared with the ZoomReader, which reads the zoomed card the same way (the
		// encounter-scenario omission applies in the zoom too: examining a card must not
		// pre-read the scenario the event panel reads when it is played).
		internal static CardInfo ExtractCard(Card card, bool includeEncounterDescription = false) {
			// A face-down card withholds its identity (the card back a sighted player sees),
			// the same rule the map and zoom readers apply. The focus path lands directly on
			// cabinet cards, where the locked court cards and locked artifacts are flipped, so
			// guarding here keeps their names from leaking. Callers that handle flipping
			// themselves (map, zoom) never reach this with a flipped card.
			if (card.Flipped)
				return new CardInfo(null, null, null, null, faceDown: true);

			bool complete = false;
			var encounter = card as EncounterCard;
			if (encounter != null) {
				// In the cabinet a court card reads as "completed" when the player has beaten
				// it, which the cabinet shows by darkening the card (its id is in the save
				// profile). EncounterCard.Complete is the in-dungeon flag and is always false
				// here, so source completion from the container's own darken in the cabinet
				// and from Complete everywhere else.
				var cabinet = card.Container as CabinetContainer;
				complete = cabinet != null ? cabinet.CheckDarken(card) : encounter.Complete;
			}

			// An encounter card's description is the encounter scenario. On the table and map
			// it is omitted: the card face shows only art and title, and the event panel reads
			// the scenario when the encounter is played, so reading it on focus would duplicate
			// it. The deck-builder zoom is the exception (it prints the scenario on its panel
			// with no event panel to double), and asks for it via includeEncounterDescription.
			string description = encounter == null || includeEncounterDescription ? card.LocalisedDescription : null;

			// Equipment traits (e.g. "Two-handed, Fast") live only on the inventory detail
			// panel, which the focus model never reaches, so fold them into the card's own
			// readout. Non-equipment cards have none.
			var equipment = card as EquipmentCard;
			string traits = equipment != null ? equipment.TraitString : null;

			// The charge counter (an artifact/consumable's remaining ability uses) shows only
			// when the quantity is non-negative; unlimited or non-ability equipment sits at -1
			// and shows no counter, and non-equipment cards have no counter at all.
			int charges = equipment != null && equipment.CardData.Quantity >= 0 ? equipment.CardData.Quantity : -1;

			// Read the "new" and "pinned" badges and the token gem off the card's live template
			// so they speak only where the game draws them. The token gem also mirrors a token
			// granted by a later stage of a sequenced encounter, which a per-card token check
			// misses. The template loads art asynchronously and can be momentarily absent, in
			// which case no badge is shown.
			bool isNew = false;
			bool pinned = false;
			bool hasToken = false;
			CardTemplate template = card.CardTemplate;
			if (template != null) {
				var newBadge = (GameObject)CardNewBadgeField.GetValue(template);
				isNew = newBadge != null && newBadge.activeSelf;
				var pinnedBadge = (UISprite)CardPinnedBadgeField.GetValue(template);
				pinned = pinnedBadge != null && pinnedBadge.enabled;
				var tokenSprite = (UISprite)CardTokenSpriteField.GetValue(template);
				hasToken = tokenSprite != null && tokenSprite.enabled;
			}

			// Card.Title is a raw localization key (e.g. ENCOUNTER_TITLE_TWISTED_CANYON);
			// there is no LocalisedTitle, so localize it here the same way the game's own
			// LocalisedDescription wraps Description. UIUtils.GetString returns the key
			// unchanged if no entry exists, so this is safe for any already-human string.
			return new CardInfo(
				UIUtils.GetString(card.Title),
				description,
				card.StatValueString,
				card.ValueString,
				hasToken,
				complete,
				traits,
				isNew: isNew,
				pinned: pinned,
				charges: charges);
		}

		private static string LabelText(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}

		private static string[] ExtractLabels(GameObject go) {
			string[] own = LabelTexts(go);
			if (HasUsableText(own))
				return own;

			// The focused object carries no label of its own: it is a bare selectable
			// whose label sits elsewhere in the owning control (UIChoiceButton, the
			// settings rows, upgrade items all wire the selectable and label as separate
			// objects). Climb to the nearest ancestor that has labels, without crossing
			// into a container that holds another selectable (which would belong to a
			// different control). Only reached when the direct sweep found nothing, so it
			// can only improve a would-be bare-name readout, never regress a working one.
			Transform t = go.transform;
			while (t.parent != null && t.parent.GetComponentsInChildren<UISelectableItem>(true).Length <= 1) {
				t = t.parent;
				string[] up = LabelTexts(t.gameObject);
				if (HasUsableText(up))
					return up;
			}
			return own;
		}

		private static string[] LabelTexts(GameObject go) {
			UILabel[] labels = go.GetComponentsInChildren<UILabel>();
			var texts = new string[labels.Length];
			for (int i = 0; i < labels.Length; i++)
				texts[i] = labels[i].text;
			return texts;
		}

		private static bool HasUsableText(string[] texts) {
			foreach (string t in texts)
				if (!string.IsNullOrEmpty(t))
					return true;
			return false;
		}
	}
}
