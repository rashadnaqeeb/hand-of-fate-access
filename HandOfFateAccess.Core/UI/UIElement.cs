using HandOfFateAccess.Localization;

namespace HandOfFateAccess.UI {
	/// <summary>
	/// A focused control as the mod understands it, built by the plugin's
	/// ProxyFactory from extracted (Unity-free) data. Describe() composes the line
	/// spoken when focus lands on it. Subclasses hold only plain data, never a live
	/// Unity component, so their composition is unit-tested off-engine.
	/// </summary>
	public abstract class UIElement {
		public abstract Message Describe();
	}

	/// <summary>
	/// The generic fallback for any control without a targeted proxy: read every
	/// label, markup stripped, empties dropped, duplicates collapsed. When a control
	/// carries no usable label text, its object name is the only identification.
	/// </summary>
	public sealed class GenericElement : UIElement {
		private readonly string _name;
		private readonly string[] _labels;

		public GenericElement(string name, string[] labels) {
			_name = name;
			_labels = labels ?? new string[0];
		}

		public override Message Describe() {
			var labels = new Message().AddRange(_labels);
			// Fall back to the object name only when no label produced spoken text.
			// The adapter (ProxyFactory) logs the diagnostic for this case, where it has
			// the live component context the name alone lacks.
			if (labels.Resolve().Length == 0)
				return new Message().Add(_name);
			return labels;
		}
	}

	/// <summary>
	/// Targeted readout for a game Card (encounter, equipment, or stat counter).
	/// Reads the data model rather than the on-card labels, so it speaks complete
	/// information (stat, rules/prompt, token stakes) instead of a jumble of
	/// template text. Order follows the announcement rules: distinguishing word
	/// (title) first, then status, the key stat, the rules/prompt, the buy/sell
	/// value, then whether a token can be won here.
	/// </summary>
	public sealed class CardElement : UIElement {
		private readonly CardInfo _info;

		public CardElement(CardInfo info) {
			_info = info;
		}

		public override Message Describe() {
			var message = new Message().Add(_info.Title);
			if (_info.Complete)
				message.Add(Strings.CardCompleted);
			message
				.Add(_info.StatValueString)
				.Add(_info.Description)
				.Add(_info.ValueString);
			// Only that a token can be won here, matching the gem on the card. The
			// reward cards are never shown to the player, so they are not announced.
			if (_info.HasToken)
				message.Add(Strings.CardToken);
			return message;
		}
	}
}
