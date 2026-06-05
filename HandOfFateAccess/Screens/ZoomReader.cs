using System.Reflection;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
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
				// There is always a confirm (reveal or the real action); cancel only when wired.
				Confirm = UIUtils.GetString(Text(ConfirmTextField, zoom)),
				Cancel = zoom.SelectableCancelAction != null ? UIUtils.GetString(Text(CancelTextField, zoom)) : null,
			};
			if (info.Flipped) return info;

			// An unseen card in the deck builder is shown as a mystery: only its title and a
			// generic "new card" line, with stat/description/lore withheld until the player
			// has encountered it. Mirror that so examining it does not spoil what a sighted
			// player cannot see.
			if (!card.Seen && Game.Instance.ActiveGameState is GameState_DeckBuilder) {
				info.Card = new CardInfo(UIUtils.GetString(card.Title), UIUtils.GetString("CARD_NEW_DESCRIPTION"), null, null);
				return info;
			}

			info.Card = ProxyFactory.ExtractCard(card);
			if (ShowsLore(card))
				info.Lore = UIUtils.GetString(card.Lore);
			info.OldItem = ReadOldItem();
			return info;
		}

		private static string LocalizeTitle(ZoomContainer zoom) {
			string key = Text(TitleField, zoom);
			if (string.IsNullOrEmpty(key)) return null;
			var args = (string[])TitleParamsField.GetValue(zoom);
			return (args != null && args.Length > 0) ? UIUtils.GetString(key, (object[])args) : UIUtils.GetString(key);
		}

		// Mirrors ZoomContainer.ShowLorePanel: lore shows in the deck builder, and for boss
		// (SpecialEncounterCard) and upgrade-reference cards anywhere. Read it only then, so
		// a card whose zoom shows no lore panel does not get lore a sighted player can't see.
		// The lore panel also hides lore for an equipment card the player cannot equip (it
		// shows a "cannot equip" warning there instead), so exclude that case too.
		private static bool ShowsLore(Card card) {
			bool panelShows = Game.Instance.ActiveGameState is GameState_DeckBuilder
				|| card is SpecialEncounterCard
				|| card is UpgradeRefCard;
			if (!panelShows) return false;
			var equipment = card as EquipmentCard;
			return equipment == null || Player.Instance.CanEquip(equipment);
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

		private static string Text(FieldInfo field, object owner) {
			return (string)field.GetValue(owner);
		}

		private static string Label(FieldInfo field, object owner) {
			var label = (UILabel)field.GetValue(owner);
			return label != null ? label.text : null;
		}
	}
}
