namespace HandOfFateAccess.UI {
	/// <summary>
	/// Plain snapshot of a focused game Card (encounter, equipment, or stat counter -
	/// all share the game's Card base), extracted by the plugin's ProxyFactory from
	/// the live model. Holds no Unity types so CardElement composition stays
	/// unit-testable. All text is RAW (markup intact); CardElement runs it through
	/// the Message/TextFilter pipeline.
	///
	/// Fields the game leaves empty for a given card (a stat counter has no tokens,
	/// a menu equipment card no ValueString) arrive as "" / empty and drop out of
	/// the spoken line.
	/// </summary>
	public sealed class CardInfo {
		/// <summary>Card name/title, the distinguishing word spoken first.</summary>
		public string Title { get; }

		/// <summary>Localized rules text (equipment) or encounter prompt (encounter card).</summary>
		public string Description { get; }

		/// <summary>Game-localized "stat: value", e.g. "Damage: 5", "Gold: 5". Empty when the card has no stat.</summary>
		public string StatValueString { get; }

		/// <summary>Buy/sell value line, empty outside shop/value contexts.</summary>
		public string ValueString { get; }

		/// <summary>True when this encounter card shows a token gem (a token can be won here).</summary>
		public bool HasToken { get; }

		/// <summary>True when an encounter card has already been resolved.</summary>
		public bool Complete { get; }

		/// <summary>
		/// True when the card is face-down (the card back a sighted player sees): its
		/// identity is withheld and only "face down card" is spoken. Used for locked
		/// cabinet cards and any other face-down card the focus path lands on.
		/// </summary>
		public bool FaceDown { get; }

		/// <summary>
		/// Equipment trait names (e.g. "Two-handed, Fast"), comma-joined by the game.
		/// Empty for non-equipment cards. Shown on the inventory detail panel, which the
		/// focus model never reaches, so it is folded into the card's own readout here.
		/// </summary>
		public string Traits { get; }

		/// <summary>True when the game badges the card as unseen ("new").</summary>
		public bool New { get; }

		/// <summary>True when the card is pinned into the deck and cannot be removed.</summary>
		public bool Pinned { get; }

		/// <summary>
		/// An equipment card's remaining ability uses (artifact/consumable charges), or -1
		/// when the card shows no charge counter (unlimited or non-ability equipment, and
		/// every non-equipment card). HasCharges gates whether it is spoken.
		/// </summary>
		public int Charges { get; }

		/// <summary>True when a charge count should be spoken.</summary>
		public bool HasCharges => Charges >= 0;

		public CardInfo(
			string title,
			string description,
			string statValueString,
			string valueString,
			bool hasToken = false,
			bool complete = false,
			string traits = null,
			bool faceDown = false,
			bool isNew = false,
			bool pinned = false,
			int charges = -1) {
			Title = title;
			Description = description;
			StatValueString = statValueString;
			ValueString = valueString;
			HasToken = hasToken;
			Complete = complete;
			Traits = traits;
			FaceDown = faceDown;
			New = isNew;
			Pinned = pinned;
			Charges = charges;
		}
	}
}
