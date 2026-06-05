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
		private static readonly TokenStake[] NoTokens = new TokenStake[0];

		/// <summary>Card name/title, the distinguishing word spoken first.</summary>
		public string Title { get; }

		/// <summary>Localized rules text (equipment) or encounter prompt (encounter card).</summary>
		public string Description { get; }

		/// <summary>Game-localized "stat: value", e.g. "Damage: 5", "Gold: 5". Empty when the card has no stat.</summary>
		public string StatValueString { get; }

		/// <summary>Buy/sell value line, empty outside shop/value contexts.</summary>
		public string ValueString { get; }

		/// <summary>Structured token stakes for an encounter card (titles gained/removed, no wording). Empty for non-encounter cards.</summary>
		public TokenStake[] Tokens { get; }

		/// <summary>True when an encounter card has already been resolved.</summary>
		public bool Complete { get; }

		public CardInfo(
			string title,
			string description,
			string statValueString,
			string valueString,
			TokenStake[] tokens = null,
			bool complete = false) {
			Title = title;
			Description = description;
			StatValueString = statValueString;
			ValueString = valueString;
			Tokens = tokens ?? NoTokens;
			Complete = complete;
		}
	}
}
