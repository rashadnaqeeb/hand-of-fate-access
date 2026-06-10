using System.Reflection;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Reads the shop's live price display. A stock card's face prints only the card's
	/// base value; the gold a transaction actually moves is ceil(value x a stock-type
	/// multiplier shaped by the shop's data, run modifiers and the Dealer's Mark rule),
	/// which the game renders on its ShopInfoPanel: the cost label while browsing
	/// stock, and the confirmation sentence ("Buy X for N gold") while a clicked card
	/// is zoomed for the decision. Both are read here live at speech time, never
	/// recomputed, so the spoken price always matches the screen. The
	/// insufficient-funds indicator rides along: it is the only reason the game ever
	/// withholds the buy action, and it shows only on the panel, which the focus path
	/// never reaches. Every stock selection updates the panel synchronously
	/// (ShopStockContainer.OnCardSelected funnels into Shop.OnStockCardSelect), so the
	/// panel always describes the card being announced.
	/// </summary>
	internal static class ShopReader {
		private static readonly FieldInfo CostValueField = AccessTools.Field(typeof(InfoPanel), "m_costValue");
		private static readonly FieldInfo InsufficientField = AccessTools.Field(typeof(ShopInfoPanel), "m_insufficient");
		private static readonly FieldInfo ConfirmationLabelField = AccessTools.Field(typeof(ShopInfoPanel), "m_confirmationLabel");
		private static readonly FieldInfo ConfirmationTransitionField = AccessTools.Field(typeof(ShopInfoPanel), "m_confirmationTransition");

		private static bool _panelWarned;

		/// <summary>
		/// The live price (and the insufficient indicator's wording, or null) for a card
		/// sitting in the shop's stock spread. False for any other card, or when the
		/// panel cannot be found.
		/// </summary>
		public static bool TryReadStockPrice(Card card, out string cost, out string insufficient) {
			cost = null;
			insufficient = null;
			Shop shop = Shop.Instance;
			if (shop == null || card.Container != shop.StockContainer) return false;
			ShopInfoPanel panel = FindPanel();
			if (panel == null) return false;
			cost = ((UILabel)CostValueField.GetValue(panel)).text;
			insufficient = ReadInsufficient(panel);
			return true;
		}

		/// <summary>
		/// The shop's transaction confirmation while a clicked stock card is zoomed: the
		/// game's own sentence carrying the real price ("Buy X for N gold"), the
		/// insufficient indicator's wording (or null), and the panel's cost line for the
		/// zoomed card's own readout. False when no confirmation is showing (any
		/// non-shop zoom, including zooms opened elsewhere while a shop is live; the
		/// confirmation auto-hides when its transaction ends, so its showing state is
		/// the gate).
		/// </summary>
		public static bool TryReadTransaction(out string prompt, out string insufficient, out string cost) {
			prompt = null;
			insufficient = null;
			cost = null;
			if (Shop.Instance == null) return false;
			ShopInfoPanel panel = FindPanel();
			if (panel == null) return false;
			var transition = (Transition)ConfirmationTransitionField.GetValue(panel);
			if (!transition.AutoShow) return false;
			prompt = ((UILabel)ConfirmationLabelField.GetValue(panel)).text;
			insufficient = ReadInsufficient(panel);
			cost = ((UILabel)CostValueField.GetValue(panel)).text;
			return true;
		}

		// The panel is a scene object the game wires only through serialized fields, so
		// it is reachable only by type. Missing while a shop is live means the price
		// readout is gone; that is logged once, not per focus change.
		private static ShopInfoPanel FindPanel() {
			var panel = Object.FindObjectOfType<ShopInfoPanel>();
			if (panel == null && !_panelWarned) {
				_panelWarned = true;
				Log.Warn("ShopInfoPanel not found while a shop is live; prices will not be spoken");
			}
			return panel;
		}

		// The indicator's own localized label when it carries one; the authored
		// fallback keeps the warning spoken if the prefab holds only a sprite.
		private static string ReadInsufficient(ShopInfoPanel panel) {
			var indicator = (GameObject)InsufficientField.GetValue(panel);
			if (!indicator.activeSelf) return null;
			UILabel label = indicator.GetComponentInChildren<UILabel>();
			string text = label != null ? label.text : null;
			return string.IsNullOrEmpty(text) ? Strings.ShopInsufficient : text;
		}
	}
}
