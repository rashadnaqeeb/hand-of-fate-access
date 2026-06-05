namespace HandOfFateAccess.UI {
	/// <summary>
	/// Plain snapshot of an encounter choice button, extracted by the plugin's
	/// ProxyFactory from the live UIChoiceButton. Holds no Unity types so ChoiceElement
	/// composition stays unit-testable. Both fields are RAW label text (markup and the
	/// game's decorative numbering intact); ChoiceElement decides ordering and cleanup.
	/// </summary>
	public sealed class ChoiceInfo {
		/// <summary>The choice's index label as shown, e.g. "1)". May be empty.</summary>
		public string Number { get; }

		/// <summary>The choice text, which the game has already formatted with the
		/// success odds, e.g. "Fight the bandits (75% chance of success)".</summary>
		public string Text { get; }

		public ChoiceInfo(string number, string text) {
			Number = number;
			Text = text;
		}
	}
}
