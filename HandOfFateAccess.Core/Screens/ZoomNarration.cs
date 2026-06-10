using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>One item in a zoom comparison (the equipped item a new one would replace),
	/// as raw label text from a display-only InfoPanel; the stat is split into a title
	/// ("Damage") and a value ("5") the way the panel splits it.</summary>
	public sealed class CompareItem {
		public string Title;
		public string StatTitle;
		public string StatValue;
		public string Description;
	}

	/// <summary>A plain snapshot of an active card zoom, extracted by the plugin's
	/// ZoomReader. The game zooms a single card for a decision (examine, equip, buy, keep,
	/// reveal...) and locks it as the sole selection, with the real detail on display-only
	/// panels and confirm/cancel having no navigable buttons; this carries everything the
	/// focus model cannot reach. All text is already localized by the adapter.</summary>
	public sealed class ZoomInfo {
		/// <summary>The card is face-down (an unseen card being examined); its identity
		/// must not be read until revealed.</summary>
		public bool Flipped;
		/// <summary>The decision prompt (e.g. "Equip this?"), or null when the zoom sets none.</summary>
		public string Title;
		/// <summary>The zoomed card, read the same way as a focused card; null when flipped.</summary>
		public CardInfo Card;
		/// <summary>The equipped item a replace decision would swap out, or null.</summary>
		public CompareItem OldItem;
		/// <summary>Flavour/lore shown on the zoom's lore panel (boss, upgrade, deck builder),
		/// or null when no lore panel is shown.</summary>
		public string Lore;
		/// <summary>The localized confirm and cancel button labels (what A/B do here, in the
		/// game's own words: "Buy"/"Back", "Smelt old"/"Smelt new"...). Null when absent.</summary>
		public string Confirm;
		public string Cancel;
		/// <summary>The localized names of the physical inputs bound to confirm and cancel
		/// on the active device ("A", "Enter Key"...), from the game's own binding tables.
		/// Null when unavailable; the hint then carries the bare label.</summary>
		public string ConfirmKey;
		public string CancelKey;
	}

	/// <summary>The spoken parts of a zoom: the decision line, and the action hint (the
	/// button labels) meant to follow it so acting quickly skips it.</summary>
	public sealed class ZoomAnnouncement {
		public string Main = "";
		public string Hint = "";
	}

	/// <summary>
	/// Composes a card zoom for speech. The decision line reads the prompt, the zoomed
	/// card, the item being replaced (if any), then any lore; the hint reads the button
	/// labels. A face-down card reads only that it is face-down (its identity is withheld
	/// until revealed). Everything runs through the Message pipeline, so empty parts drop
	/// and markup is stripped; an inactive zoom composes to empty.
	/// </summary>
	public static class ZoomNarration {
		public static ZoomAnnouncement Compose(ZoomInfo z) {
			var ann = new ZoomAnnouncement();
			if (z == null) return ann;
			if (z.Flipped) {
				ann.Main = new Message().Add(Strings.CardFaceDown).Resolve();
				ann.Hint = Hint(z);
				return ann;
			}
			var message = new Message().Add(z.Title);
			if (z.Card != null)
				message.Add(new CardElement(z.Card).Describe().Resolve());
			if (z.OldItem != null)
				message.Add(Replacing(z.OldItem));
			message.Add(z.Lore);
			ann.Main = message.Resolve();
			ann.Hint = Hint(z);
			return ann;
		}

		private static string Hint(ZoomInfo z) {
			return new Message().Add(Action(z.Confirm, z.ConfirmKey)).Add(Action(z.Cancel, z.CancelKey)).Resolve();
		}

		// "Equip: A" -- the action word leads (it is the varying, decision-relevant part),
		// the bound key's name follows so the player knows which physical input takes the
		// action; the zoom has no navigable buttons through which to discover that. Both
		// words come from the game, so no authored string is needed, only punctuation.
		// Shared with ChoiceSetElement (the whole-set keep-or-redraw choice), which has
		// the same buttonless confirm/cancel.
		public static string Action(string label, string key) {
			if (string.IsNullOrEmpty(label)) return null;
			return string.IsNullOrEmpty(key) ? label : label + ": " + key;
		}

		private static string Replacing(CompareItem item) {
			string line = ItemLine(item);
			return line.Length == 0 ? "" : Strings.ZoomReplacing + " " + line;
		}

		private static string ItemLine(CompareItem item) {
			return new Message()
				.Add(item.Title)
				.Add(Stat(item.StatTitle, item.StatValue))
				.Add(item.Description)
				.Resolve();
		}

		private static string Stat(string title, string value) {
			string t = (title ?? "").Trim();
			string v = (value ?? "").Trim();
			if (t.Length == 0) return v;
			if (v.Length == 0) return t;
			return t + " " + v;
		}
	}
}
