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
	/// (title) first, then status, the key stat, the rules/prompt, equipment traits,
	/// the buy/sell value, then whether a token can be won here.
	/// </summary>
	public sealed class CardElement : UIElement {
		private readonly CardInfo _info;

		public CardElement(CardInfo info) {
			_info = info;
		}

		public override Message Describe() {
			// A face-down card withholds its identity (the card back a sighted player
			// sees), matching the map and zoom readers.
			if (_info.FaceDown)
				return new Message().Add(Strings.CardFaceDown);
			var message = new Message().Add(_info.Title);
			if (_info.Complete)
				message.Add(Strings.CardCompleted);
			message
				.Add(_info.StatValueString)
				.Add(_info.Description)
				.Add(_info.Traits)
				.Add(_info.ValueString);
			// Only that a token can be won here, matching the gem on the card. The
			// reward cards are never shown to the player, so they are not announced.
			if (_info.HasToken)
				message.Add(Strings.CardToken);
			return message;
		}
	}

	/// <summary>
	/// Readout for an empty equipment slot in the paperdoll. A filled slot forwards
	/// focus to its card (read as a CardElement); an empty one is sprite-only with no
	/// label, so without this it would speak only its raw object name. The category
	/// (weapon, armour...) comes first as the distinguishing word across slots, then
	/// that the slot is empty. The category text is the game's own localized title,
	/// extracted by the adapter; only the "empty" suffix is mod-authored.
	/// </summary>
	public sealed class EquipmentSlotElement : UIElement {
		private readonly string _category;

		public EquipmentSlotElement(string category) {
			_category = category;
		}

		public override Message Describe() {
			return new Message().Add(_category).Add(Strings.SlotEmpty);
		}
	}

	/// <summary>
	/// Readout for a focused map slot. The top card (the encounter) is read first as a
	/// normal card, or as face-down when hidden; then each card stacked under it by a
	/// spice event is read as an attached item ("with Apple, restores 5 food"). This
	/// replaces the generic label sweep that read every card on the slot as one
	/// undifferentiated jumble, while still surfacing the attached cards a sighted player
	/// sees peeking out, in order and clearly framed.
	/// </summary>
	public sealed class MapSlotElement : UIElement {
		private readonly MapSlotInfo _info;

		public MapSlotElement(MapSlotInfo info) {
			_info = info;
		}

		public override Message Describe() {
			var message = new Message();
			message.Add(Face(_info.Top));
			foreach (CardFace spice in _info.Spices) {
				string line = Face(spice);
				if (line.Length > 0)
					message.Add(Strings.MapSlotAttached + " " + line);
			}
			return message;
		}

		// A face-down card withholds its identity (the card back a sighted player sees);
		// a face-up one reads as a normal card.
		private static string Face(CardFace face) {
			if (face.FaceDown)
				return Strings.CardFaceDown;
			return new CardElement(face.Card).Describe().Resolve();
		}
	}

	/// <summary>
	/// Readout for a focused deck-builder pile on the select-mode screen. The pile's
	/// title comes first as the distinguishing word; for the limited piles (equipment,
	/// encounter) the card count toward the required limit follows, then a status word
	/// when the deck is off that limit. This mirrors the game's DeckInfoPanel, which
	/// shows the same title, an "N/M" counter, and an insufficient / too-many icon. The
	/// Archetype pile has no count and reads as title only. A count exactly on the limit
	/// reads without a status word; the equal count already conveys it.
	/// </summary>
	public sealed class DeckPileElement : UIElement {
		private readonly DeckPileInfo _info;

		public DeckPileElement(DeckPileInfo info) {
			_info = info;
		}

		public override Message Describe() {
			var message = new Message().Add(_info.Title);
			if (_info.HasCount) {
				message.Add(_info.Count + "/" + _info.Limit);
				if (_info.Count < _info.Limit)
					message.Add(Strings.DeckInsufficient);
				else if (_info.Count > _info.Limit)
					message.Add(Strings.DeckTooMany);
			}
			return message;
		}
	}

	/// <summary>
	/// Readout for an encounter choice button. The number comes first: the game's focus
	/// skips disabled/unavailable choices (they are non-selectable), so a gap in the
	/// spoken numbers (1 then 3) is the only cue to the player that a choice was passed
	/// over. This is the deliberate exception to the no-positional-counts rule. The
	/// game's decorative ")" on the number is trimmed so the reader does not announce it.
	/// </summary>
	public sealed class ChoiceElement : UIElement {
		private readonly ChoiceInfo _info;

		public ChoiceElement(ChoiceInfo info) {
			_info = info;
		}

		public override Message Describe() {
			string number = _info.Number;
			if (!string.IsNullOrEmpty(number))
				number = number.TrimEnd(')', ' ');
			return new Message().Add(number).Add(_info.Text);
		}
	}
}
