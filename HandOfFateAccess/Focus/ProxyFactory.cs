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
		private static readonly FieldInfo ChoiceRevealGroupField = AccessTools.Field(typeof(CardChoiceContainer), "m_revealSelectableGroup");
		private static readonly FieldInfo SetChoiceGroupField = AccessTools.Field(typeof(CardChoiceContainer), "m_setChoiceSelectableGroup");
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
		// A monster card's creature name (the suit shown on its face) is the private title
		// key the game feeds its template; MonsterCard has no public getter for it alone
		// (LocalisedTitle bakes in the count), so it is read here to compose the title.
		private static readonly FieldInfo MonsterCreatureField = AccessTools.Field(typeof(MonsterCard), "m_cardTitle");
		// The reward modifier screen's add button and its banner label, both private. The
		// banner holds the localized "what is being added" text the game sets when the reward
		// set is assigned; the add selectable identifies the button so it is not confused
		// with the reward cards in the same container.
		private static readonly FieldInfo ModifierAddSelectableField = AccessTools.Field(typeof(CardSetModifierContainer), "m_addSelectable");
		private static readonly FieldInfo ModifierTitleField = AccessTools.Field(typeof(CardSetModifierContainer), "m_titleText");
		// Settings rows: each control class holds the value label it rewrites in place
		// as a private serialized field, mapped here per type so the value can be
		// separated from the row title for value-only re-announcements. One table so
		// each row type is declared exactly once; a row carries exactly one of these.
		// The volume slider is separate: its value is a UISlider fill, not a label.
		private static readonly FieldInfo VolumeSliderField = AccessTools.Field(typeof(VolumeSlider), "m_slider");
		private static readonly KeyValuePair<System.Type, FieldInfo>[] SettingValueFields = {
			SettingField(typeof(SubtitleToggle), "m_label"),
			SettingField(typeof(UIProfileKeyDataToggle), "m_valueLabel"),
			SettingField(typeof(UIGraphicsSettingsSelector), "m_label"),
			SettingField(typeof(UIGraphicsSettingsResolution), "m_resolutionLabel"),
			SettingField(typeof(UIGraphicsSettingsQuality), "m_valueLabel"),
			SettingField(typeof(UIGraphicsSettingsFullScreen), "m_valueLabel"),
			SettingField(typeof(UIGraphicsSettingsImageEffectToggle), "m_valueLabel"),
			SettingField(typeof(LanguageVOSelect), "m_languageLabel"),
		};

		private static KeyValuePair<System.Type, FieldInfo> SettingField(System.Type type, string field) {
			return new KeyValuePair<System.Type, FieldInfo>(type, AccessTools.Field(type, field));
		}
		// A key-binding row's action and bound-key labels, both private; the focused
		// key button's own label is only the key name.
		private static readonly FieldInfo BindingActionField = AccessTools.Field(typeof(ControlBindElement), "m_actionLabel");
		private static readonly FieldInfo BindingKeyField = AccessTools.Field(typeof(ControlBindElement), "m_primaryLabel");

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
			if (selectable != null && (IsBlockerFocus(selectable) || IsChoiceRevealFocus(selectable)))
				return null;

			// The whole-set card choice (keep all the dealt cards, or redraw the set with
			// cancel) locks selection to a single label-less placeholder while the set lies
			// face-up on the table, so the cards are never focusable and the placeholder
			// would read as its bare object name. Compose the whole decision instead: the
			// container's prompt, every card in the set, then the confirm and cancel
			// actions with their bound keys. Matched by selectable group, like the reveal
			// placeholder.
			if (selectable != null && IsSetChoiceFocus(selectable))
				return SetChoiceElement(selectable);

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
			// empty slot yields null here and falls through to generic handling. The exits
			// (which directions hold a neighbouring card) follow the cards, same as the
			// free-roam cursor's readout, so the game cursor also teaches the board shape.
			MapLayoutSlot mapSlot = go.GetComponentInParent<MapLayoutSlot>();
			if (mapSlot != null) {
				MapSlotInfo slotInfo = MapSlotReader.Read(mapSlot);
				if (slotInfo != null) {
					Vector2 grid = mapSlot.GridPosition;
					MapExits exits = Maps.MapBoardReader.ReadExits(
						Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.y));
					return new MapSlotElement(slotInfo, exits);
				}
			}

			// An archetype (Fates) card's detail lives on the deck builder's CabinetCardInfo
			// panel (description, upgrades, deck changes, loadout), which the ArchetypeReader
			// owns and edge-announces. The focused card itself would only re-read the title and
			// duplicate the panel's description, so suppress it here, the same way the zoom is.
			// Checked before the Card branch since an archetype card is a Card.
			if (go.GetComponentInParent<ArchetypeCard>() != null)
				return null;

			Card card = go.GetComponentInParent<Card>();
			if (card != null) {
				CardInfo info = ExtractCard(card);
				// A shop stock card's face prints the base value, not the gold the
				// purchase or sale actually moves; swap in the live price from the
				// shop's info panel, with its insufficient-funds indicator. Applies
				// only to cards in the stock spread (the shop's menu cards and any
				// other card focused while a shop is live keep their normal readout).
				if (ShopReader.TryReadStockPrice(card, out string cost, out string insufficient))
					info = info.WithShopPrice(cost, insufficient);
				return new CardElement(info);
			}

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

			// An equipment slot in the paperdoll is a CardContainer that holds its equipped
			// card(s) as child objects, so the Card branch above (GetComponentInParent) never
			// reaches them and the slot itself carries only sprites, no label. Read the slot's
			// own cards through the card model, the same way the cabinet container is read; an
			// empty slot has none and reads as empty rather than falling through to its bare
			// object name ("PaperdollSlot(Clone)"). The single-equip slots (weapon, armour...)
			// hold one card; the trinket and modifier slots hold several. The category comes
			// from the game's own localized title (GetCategoryTitle returns a localization key;
			// UIUtils.GetString localizes it, returning "" for none).
			PaperdollSlot slot = go.GetComponent<PaperdollSlot>();
			if (slot != null) {
				string category = UIUtils.GetString(InventoryCategoryData.GetCategoryTitle(slot.CategoryData.type));
				var cards = new List<CardInfo>();
				foreach (Card equipped in slot.Cards)
					cards.Add(ExtractCard(equipped));
				return new EquipmentSlotElement(category, cards);
			}

			// The inventory's owned-item list slots and the inspect/compare spread are
			// CardContainers that hold their card as a child, so the Card branch above
			// (GetComponentInParent) never reaches them and they would fall through to their
			// bare object name. Read the shown card through the card model the same way the
			// cabinet is read. Focus landing directly on a selectable card is already handled
			// by the Card branch above, so these only fire when focus rests on the container.
			// A list slot holds a single owned card.
			InventorySlotContainer listSlot = go.GetComponentInParent<InventorySlotContainer>();
			if (listSlot != null && listSlot.TopCard != null)
				return new CardElement(ExtractCard(listSlot.TopCard));

			// The inspect spread stacks the clicked card plus every other owned card, but
			// hides all but the first (the game fades index > 0 to alpha 0 and treats Cards[0]
			// as the inspected card), so read the first card, not the last.
			InventoryInspectContainer inspect = go.GetComponentInParent<InventoryInspectContainer>();
			if (inspect != null && inspect.Cards.Count > 0)
				return new CardElement(ExtractCard(inspect.Cards[0]));

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

			// The end-of-run scoreboard locks selection onto a single continue control that has
			// no label of its own, so it would fall to a bare object name. Its only action is to
			// proceed past the results, so announce "Continue". The score breakdown itself is
			// read off the display-only labels by ScoreboardReader.
			if (go.GetComponentInParent<Scoreboard>() != null)
				return new GenericElement(Strings.ControlContinue, EmptyLabels);

			// The dungeon select state on a level-0 profile has no choice to offer (the
			// story/endless pair needs Player.Level > 0), so it shows the game's InputScreen
			// instead: a single full-screen label-less selectable (scene name "Item") whose
			// only action is to continue into the story dungeon. The name is too generic for
			// the NameReadouts table, so resolve the owning InputScreen structurally.
			if (go.GetComponentInParent<InputScreen>() != null)
				return new GenericElement(Strings.ControlContinue, EmptyLabels);

			// A reward token won at the end of a run is a 3D gem prop with a UISelectableItem
			// but no label, so it falls to its raw object name ("Token_WhiteMinotaur5(Clone)").
			// The game gives it no localized name; a sighted player recognises it by its gem
			// art. Resolve the localized title of the card that grants it (whose token sprite
			// is that gem art) and pass it with the raw object name; RewardTokenElement speaks
			// the title verbatim (it already carries the tier number), or synthesises a name
			// from the id when no granter is found. The cards it grants stay hidden until the
			// player activates it and flips them, surfaced then by the per-card reveal.
			Token token = go.GetComponentInParent<Token>();
			if (token != null)
				return new RewardTokenElement(TokenReader.GrantingTitle(token), token.gameObject.name);

			// The reward modifier screen's "add to deck" button is a label-less UISelectableItem
			// that falls to its raw object name ("AddSelectable"). The reward cards in the same
			// container are read by the Card branch above, so reaching here on this container is
			// the add button; confirm against the add selectable to be sure, then read the banner
			// (what is being added, shown above the button) plus the authored action word.
			CardSetModifierContainer modifier = go.GetComponentInParent<CardSetModifierContainer>();
			if (modifier != null) {
				var addSelectable = (UISelectableItem)ModifierAddSelectableField.GetValue(modifier);
				if (addSelectable != null && go == addSelectable.gameObject) {
					var bannerLabel = (UILabel)ModifierTitleField.GetValue(modifier);
					return new RewardAddElement(bannerLabel != null ? bannerLabel.text : null);
				}
			}

			// A volume slider row draws its value as the slider's fill sprite with no
			// text, so the generic sweep spoke only the row title and an adjustment spoke
			// nothing at all. Read the slider's live 0..1 value; SliderElement words it
			// as a percentage.
			VolumeSlider volume = go.GetComponentInParent<VolumeSlider>();
			if (volume != null) {
				var slider = (UISlider)VolumeSliderField.GetValue(volume);
				return new SliderElement(ExtractLabels(go), slider.value);
			}

			// A key-binding row focuses the key button, whose own label is only the key
			// name, so the generic sweep dropped the action being bound. Read the row's
			// action and key labels directly. An invalid binding (the same key bound to
			// two actions) shows only as a red tint, so the flag rides along for
			// BindingElement to word.
			ControlBindElement binding = go.GetComponentInParent<ControlBindElement>();
			if (binding != null)
				return new BindingElement(LabelText(BindingActionField, binding), LabelText(BindingKeyField, binding), binding.Invalid);

			// The remaining settings rows (on/off toggles and left/right selectors)
			// rewrite a value label in place. The generic sweep already read title and
			// value, but separating the value lets a left/right change re-announce just
			// the new value instead of the whole row. The value label is excluded from
			// the title sweep so it is not doubled.
			UILabel settingValue = SettingValueLabel(go);
			if (settingValue != null)
				return new SettingElement(ExtractLabels(go, settingValue), settingValue.text);

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

		// While a card choice flips its unseen cards face-up (CardChoiceContainer.Reveal),
		// the game force-selects a label-less placeholder whose only click action skips
		// the flip animation, so it would read as its bare object name. The cards are
		// read the moment the reveal ends either way (a pick-one choice focuses them, the
		// whole-set choice folds them into its readout), so the placeholder carries
		// nothing worth speaking. Matched by its selectable group on the live choice
		// container, not by name or hierarchy. DeckManager.Instance is legitimately null
		// outside a run (menus), where no card choice can be up.
		private static bool IsChoiceRevealFocus(UISelectable selectable) {
			if (selectable.Group == null) return false;
			DeckManager deck = DeckManager.Instance;
			if (deck == null) return false;
			return selectable.Group == (UISelectableGroup)ChoiceRevealGroupField.GetValue(deck.CardChoiceContainer);
		}

		private static bool IsSetChoiceFocus(UISelectable selectable) {
			if (selectable.Group == null) return false;
			DeckManager deck = DeckManager.Instance;
			if (deck == null) return false;
			return selectable.Group == (UISelectableGroup)SetChoiceGroupField.GetValue(deck.CardChoiceContainer);
		}

		// The prompt is the container's context title (the same field the zoom prompt
		// lives in); the action labels are the focused placeholder's own context texts,
		// public on UISelectable, falling back to its group's where the game set them.
		// The bound key names resolve the same way as the zoom hint, since neither
		// surface has navigable buttons.
		private static UIElement SetChoiceElement(UISelectable selectable) {
			CardChoiceContainer container = DeckManager.Instance.CardChoiceContainer;
			var cards = new List<CardInfo>();
			foreach (Card card in container.Cards)
				cards.Add(ExtractCard(card));
			string confirm = UIUtils.GetString(selectable.SelectContextText);
			string cancel = UIUtils.GetString(selectable.CancelContextText);
			string confirmKey = null;
			string cancelKey = null;
			UIManager ui = UIManager.Instance;
			MainNavBar bar = ui != null ? ui.PrimaryNavBar : null;
			if (bar != null) {
				confirmKey = ZoomReader.BoundKeyName(bar.ConfirmButton, ZoomReader.ConfirmAction);
				cancelKey = ZoomReader.BoundKeyName(bar.CancelButton, ZoomReader.CancelAction);
			}
			return new ChoiceSetElement(ZoomReader.LocalizeTitle(container), cards, confirm, confirmKey, cancel, cancelKey);
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
		internal static CardInfo ExtractCard(Card card, bool includeEncounterDescription = false, bool ignoreFlippedFaceDown = false) {
			// A face-down card withholds its identity (the card back a sighted player sees).
			// In the cabinet and zoom the Flipped flag marks the hidden state: the focus path
			// lands directly on cabinet cards, where locked court cards and locked artifacts are
			// flipped, so guarding here keeps their names from leaking. The map is the exception:
			// it decides face-up/down from board orientation (where a flipped card can be face-UP)
			// and passes ignoreFlippedFaceDown once it has judged a card visible, so this guard
			// does not then re-hide it. Other callers leave the flag false and keep Flipped=hidden.
			if (card.Flipped && !ignoreFlippedFaceDown)
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
			// A monster card has no description on its face at all.
			var monster = card as MonsterCard;
			string description = monster != null ? null
				: encounter == null || includeEncounterDescription ? card.LocalisedDescription : null;

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
			// unchanged if no entry exists, so this is safe for any already-human string. A
			// monster card's title is the composed playing-card header instead.
			// The buy/sell value renders as "$<value>", so a card with no sale value
			// (m_value 0, e.g. a gold/supply reward card) would speak a meaningless "$0".
			// Match the game's own CanSell (m_value > 0) and drop it when there is none.
			string valueString = card.Value > 0 ? card.ValueString : null;

			return new CardInfo(
				monster != null ? MonsterTitle(monster) : UIUtils.GetString(card.Title),
				description,
				card.StatValueString,
				valueString,
				hasToken,
				complete,
				traits,
				isNew: isNew,
				pinned: pinned,
				charges: charges);
		}

		// A monster card's title is the playing-card header "N of Creature" (number card) or
		// "Jack/Queen/King of Creature" (face card). MonsterCard.LocalisedTitle cannot be used:
		// it always builds the count form, so a face card reads "1 of Creature". Compose it from
		// the model instead. The rank is the creature count for a number card, or the face rank
		// for a face card; the connective and ranks are the game's own localized strings (the
		// same keys MonsterCard.Build feeds the template), so the phrase matches the card face
		// and stays translatable. Shared by the focus/zoom path and the combat-roster reader.
		internal static string MonsterTitle(MonsterCard monster) {
			string creature = UIUtils.GetString((string)MonsterCreatureField.GetValue(monster));
			string connector = UIUtils.GetString("MONSTER_CARD_TITLE_OF");
			string rank;
			switch (monster.Face) {
				case MonsterCard.FaceType.Jack: rank = UIUtils.GetString("MONSTER_CARD_TITLE_JACK"); break;
				case MonsterCard.FaceType.Queen: rank = UIUtils.GetString("MONSTER_CARD_TITLE_QUEEN"); break;
				case MonsterCard.FaceType.King: rank = UIUtils.GetString("MONSTER_CARD_TITLE_KING"); break;
				default: rank = monster.Count.ToString(); break;
			}
			return MonsterNarration.Title(rank, connector, creature);
		}

		private static string LabelText(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}

		// The value label of a settings row, resolved through the type-to-field table;
		// null for a focused object that is not on one.
		private static UILabel SettingValueLabel(GameObject go) {
			foreach (KeyValuePair<System.Type, FieldInfo> entry in SettingValueFields) {
				Component owner = go.GetComponentInParent(entry.Key);
				if (owner != null)
					return (UILabel)entry.Value.GetValue(owner);
			}
			return null;
		}

		private static string[] ExtractLabels(GameObject go, UILabel exclude = null) {
			string[] own = LabelTexts(go, exclude);
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
				string[] up = LabelTexts(t.gameObject, exclude);
				if (HasUsableText(up))
					return up;
			}
			return own;
		}

		private static string[] LabelTexts(GameObject go, UILabel exclude) {
			UILabel[] labels = go.GetComponentsInChildren<UILabel>();
			var texts = new List<string>(labels.Length);
			foreach (UILabel label in labels)
				if (label != exclude)
					texts.Add(label.text);
			return texts.ToArray();
		}

		private static bool HasUsableText(string[] texts) {
			foreach (string t in texts)
				if (!string.IsNullOrEmpty(t))
					return true;
			return false;
		}
	}
}
