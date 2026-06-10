using System;
using System.Reflection;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the card-zoom overlay: the single card the game presents for a decision
	/// (examine, equip, buy, keep, reveal) plus its decision prompt, button labels, the
	/// item a replace would swap out, and any lore. The zoom locks that one card as the
	/// sole selection and puts the real detail on display-only panels, so the focus path
	/// cannot read it (the card is deduped or mid-animation, and the panels carry no
	/// selectable); this reads it all off the model directly. Reached from the live
	/// DeckManager.ZoomContainer; private fields are reached by reflection so a game-side
	/// rename crashes the build's audit. Values are read live; nothing is cached.
	/// </summary>
	internal static class ZoomReader {
		// The active context labels live on CardContainer and reflect what the zoom set
		// (including a flipped card's "Reveal"), so they are the source of truth for the
		// prompt and button text rather than ZoomContainer's raw inputs.
		private static readonly FieldInfo TitleField = AccessTools.Field(typeof(CardContainer), "m_selectableContextTitle");
		private static readonly FieldInfo TitleParamsField = AccessTools.Field(typeof(CardContainer), "m_selectableContextTitleParams");
		private static readonly FieldInfo ConfirmTextField = AccessTools.Field(typeof(CardContainer), "m_selectableContextText");
		private static readonly FieldInfo CancelTextField = AccessTools.Field(typeof(CardContainer), "m_cancelContextText");
		// The zoom wires its buttons as per-card actions (CardClickAction/CardCancelAction),
		// not the Selectable* ones, so these are the presence signals for confirm/cancel. A
		// confirm is not guaranteed: the deck-builder zoom is examine-only (cancel/back, no
		// confirm), and a flipped card's confirm is the reveal action.
		private static readonly FieldInfo ClickActionField = AccessTools.Field(typeof(CardContainer), "m_cardClickAction");
		private static readonly FieldInfo CancelActionField = AccessTools.Field(typeof(CardContainer), "m_cardCancelAction");
		private static readonly FieldInfo OldPanelField = AccessTools.Field(typeof(ComparePanelManager), "m_old");
		private static readonly FieldInfo InfoTitleField = AccessTools.Field(typeof(InfoPanel), "m_title");
		private static readonly FieldInfo InfoStatTitleField = AccessTools.Field(typeof(InfoPanel), "m_statTitle");
		private static readonly FieldInfo InfoStatValueField = AccessTools.Field(typeof(InfoPanel), "m_statValue");
		private static readonly FieldInfo InfoDescriptionField = AccessTools.Field(typeof(InfoPanel), "m_description");

		/// <summary>The active zoom, or null when no card is zoomed.</summary>
		public static ZoomInfo Read() {
			DeckManager deck = DeckManager.Instance;
			ZoomContainer zoom = deck != null ? deck.ZoomContainer : null;
			if (zoom == null) return null;
			Card card = zoom.TopCard;
			if (card == null) return null;

			var info = new ZoomInfo {
				Flipped = card.Flipped,
				Title = LocalizeTitle(zoom),
				Confirm = HasAction(ClickActionField, zoom) ? UIUtils.GetString(Text(ConfirmTextField, zoom)) : null,
				Cancel = HasAction(CancelActionField, zoom) ? UIUtils.GetString(Text(CancelTextField, zoom)) : null,
			};
			UIManager ui = UIManager.Instance;
			MainNavBar bar = ui != null ? ui.PrimaryNavBar : null;
			if (bar != null) {
				if (info.Confirm != null) info.ConfirmKey = BoundKeyName(bar.ConfirmButton);
				if (info.Cancel != null) info.CancelKey = BoundKeyName(bar.CancelButton);
			}
			if (info.Flipped) return info;

			// An unseen card in the deck builder is shown as a mystery: only its title and a
			// generic "new card" line, with stat/description/lore withheld until the player
			// has encountered it. Mirror that so examining it does not spoil what a sighted
			// player cannot see.
			if (!card.Seen && Game.Instance.ActiveGameState is GameState_DeckBuilder) {
				info.Card = new CardInfo(UIUtils.GetString(card.Title), UIUtils.GetString("CARD_NEW_DESCRIPTION"), null, null);
				return info;
			}

			// A seen equipment card prints its stat, rules, traits, value and charges on its
			// face, so focus already read them; the deck-builder examine zoom adds only the lore,
			// or the cannot-equip warning that replaces it when the archetype's locked gear owns
			// the slot. Carry just the title and that one line. Other equipment zooms (the
			// in-dungeon equip or shop buy decision, where PanelShows is false) keep the full
			// readout, since the player may not have focused the card first and needs the stats.
			var equipment = card as EquipmentCard;
			if (equipment != null && PanelShows(card)) {
				info.Card = new CardInfo(UIUtils.GetString(card.Title), null, null, null);
				info.Lore = Player.Instance.CanEquip(equipment)
					? UIUtils.GetString(card.Lore)
					: UIUtils.GetString("ARCHETYPE_CANNOT_EQUIP");
			} else {
				// The deck-builder zoom prints the card's description on its panel, so include the
				// encounter scenario the focus path omits (no event panel doubles it here).
				info.Card = ProxyFactory.ExtractCard(card, includeEncounterDescription: PanelShows(card));
				if (ShowsLore(card))
					info.Lore = ComposeLore(card);
			}
			info.OldItem = ReadOldItem();
			return info;
		}

		// The zoom locks its card as the sole selection, so confirm/cancel are taken with
		// the bound inputs directly, not through navigable buttons; the hint names those
		// inputs. The nav bar's confirm/cancel buttons each carry a UIInputSprite whose
		// public button-string properties resolve the live binding for the active device
		// (controller type and keyboard rebinds included), the same path the game's
		// tutorials use to compose "press X" text. A failed lookup drops the key name
		// (the hint then carries the bare label) and is logged once, not per frame.
		private static bool _keyNameWarned;

		private static string BoundKeyName(NavBarButton button) {
			if (button == null) return null;
			UIInputSprite sprite = button.GetComponent<UIInputSprite>();
			if (sprite == null) return null;
			try {
				string name = InputManager.UseGamepad ? sprite.ControllerButtonString : sprite.KMButtonString;
				return string.IsNullOrEmpty(name) ? null : name;
			} catch (Exception ex) {
				if (!_keyNameWarned) {
					_keyNameWarned = true;
					Log.Warn("zoom hint key name lookup failed, hint will carry labels only: " + ex);
				}
				return null;
			}
		}

		private static string LocalizeTitle(ZoomContainer zoom) {
			string key = Text(TitleField, zoom);
			if (string.IsNullOrEmpty(key)) return null;
			var args = (string[])TitleParamsField.GetValue(zoom);
			return (args != null && args.Length > 0) ? UIUtils.GetString(key, (object[])args) : UIUtils.GetString(key);
		}

		// Mirrors ZoomContainer.ShowLorePanel: the zoom builds its description/lore panel in the
		// deck builder for any card, and for boss (SpecialEncounterCard) and upgrade-reference
		// cards anywhere. The panel prints the card's description, so its presence is also what
		// makes an encounter's scenario visible (and worth reading) in the deck-builder zoom.
		private static bool PanelShows(Card card) {
			return Game.Instance.ActiveGameState is GameState_DeckBuilder
				|| card is SpecialEncounterCard
				|| card is UpgradeRefCard;
		}

		// Lore shows on that panel too, so read it only then (a card whose zoom shows no panel
		// must not get lore a sighted player cannot see). The panel hides lore for an equipment
		// card the player cannot equip (a "cannot equip" warning takes its place), so exclude that.
		private static bool ShowsLore(Card card) {
			if (!PanelShows(card)) return false;
			var equipment = card as EquipmentCard;
			return equipment == null || Player.Instance.CanEquip(equipment);
		}

		// The lore panel appends a non-special encounter's category names ("Combat, Event") to
		// its lore label, so fold them into the lore the same way for parity.
		private static string ComposeLore(Card card) {
			string lore = UIUtils.GetString(card.Lore);
			var encounter = card as EncounterCard;
			if (encounter == null || encounter is SpecialEncounterCard)
				return lore;
			var categories = encounter.EncounterPrefab.CategoryStrings;
			if (categories == null || categories.Count == 0)
				return lore;
			string joined = string.Empty;
			for (int i = 0; i < categories.Count; i++) {
				if (i > 0)
					joined += ", ";
				joined += UIUtils.GetString(categories[i]);
			}
			return string.IsNullOrEmpty(lore) ? joined : lore + ", " + joined;
		}

		// The equipped item a replace would swap out, shown on the old compare panel; only
		// present for equip-replace prompts.
		private static CompareItem ReadOldItem() {
			UIManager ui = UIManager.Instance;
			ComparePanelManager compare = ui != null ? ui.ComparePanelManager : null;
			if (compare == null) return null;
			var oldPanel = (InfoPanel)OldPanelField.GetValue(compare);
			if (oldPanel == null || oldPanel.Transition == null || !oldPanel.Transition.AutoShow) return null;
			return new CompareItem {
				Title = Label(InfoTitleField, oldPanel),
				StatTitle = Label(InfoStatTitleField, oldPanel),
				StatValue = Label(InfoStatValueField, oldPanel),
				Description = Label(InfoDescriptionField, oldPanel),
			};
		}

		private static bool HasAction(FieldInfo field, object owner) {
			return field.GetValue(owner) != null;
		}

		private static string Text(FieldInfo field, object owner) {
			return (string)field.GetValue(owner);
		}

		private static string Label(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}
	}
}
