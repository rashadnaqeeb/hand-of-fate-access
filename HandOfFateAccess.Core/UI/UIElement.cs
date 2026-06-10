using HandOfFateAccess.Localization;
using HandOfFateAccess.Screens;

namespace HandOfFateAccess.UI {
	/// <summary>
	/// A focused control as the mod understands it, built by the plugin's
	/// ProxyFactory from extracted (Unity-free) data. Describe() composes the line
	/// spoken when focus lands on it. Subclasses hold only plain data, never a live
	/// Unity component, so their composition is unit-tested off-engine.
	/// </summary>
	public abstract class UIElement {
		public abstract Message Describe();

		/// <summary>
		/// The value part of the readout alone, for re-announcing an in-place value
		/// change on a control that separates its identity from its value (a settings
		/// row whose value moves on left/right while focus stays put). The title did
		/// not change and was spoken when focus landed, so only the value is repeated.
		/// Null when the element has no separable value; the full Describe() line is
		/// re-spoken instead.
		/// </summary>
		public virtual Message DescribeValue() {
			return null;
		}
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
			if (_info.New)
				message.Add(Strings.CardNew);
			// Pinned cards cannot be taken out of the deck; the player needs to know before
			// trying. Silence means the card can be removed.
			if (_info.Pinned)
				message.Add(Strings.CardPinned);
			message.Add(_info.StatValueString);
			// The charge count follows the stat (and is the key number for artifacts, which
			// carry no stat), ahead of the rules text that says what the ability does.
			if (_info.HasCharges)
				message.Add(_info.Charges + " " + (_info.Charges == 1 ? Strings.CardCharge : Strings.CardCharges));
			message
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
	/// Readout for a paperdoll equipment slot. The slot is sprite-only with no label
	/// of its own, and its equipped card(s) sit as child objects the focus path cannot
	/// reach, so without this it would speak only its raw object name. The category
	/// (weapon, armour...) comes first as the distinguishing word across slots, then
	/// each equipped card read as a normal card, or "empty" when nothing is equipped.
	/// The single-equip slots hold one card; the trinket and modifier slots hold
	/// several. The category text is the game's own localized title, extracted by the
	/// adapter; only the "empty" suffix is mod-authored.
	/// </summary>
	public sealed class EquipmentSlotElement : UIElement {
		private readonly string _category;
		private readonly System.Collections.Generic.IList<CardInfo> _cards;

		public EquipmentSlotElement(string category, System.Collections.Generic.IList<CardInfo> cards) {
			_category = category;
			_cards = cards;
		}

		public override Message Describe() {
			var message = new Message().Add(_category);
			if (_cards == null || _cards.Count == 0)
				return message.Add(Strings.SlotEmpty);
			foreach (CardInfo card in _cards)
				message.Add(new CardElement(card).Describe().Resolve());
			return message;
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
	/// Readout for an end-of-run reward token: a gem prop the player activates to unlock the
	/// cards it grants. It has no label and no localized name, so the adapter resolves the
	/// localized title of the card that grants it (the token's gem art is that card's token
	/// sprite) and passes that plus the raw object id; TokenNarration composes the spoken name
	/// (the granting title verbatim, which already carries the tier number, or an id-synthesised
	/// name when no granter was found). The authored "reward token" follows so the type is
	/// clear. When neither source yields a name, only the type word is spoken.
	/// </summary>
	public sealed class RewardTokenElement : UIElement {
		private readonly string _grantingTitle;
		private readonly string _rawId;

		public RewardTokenElement(string grantingTitle, string rawId) {
			_grantingTitle = grantingTitle;
			_rawId = rawId;
		}

		public override Message Describe() {
			return new Message().Add(TokenNarration.Compose(_grantingTitle, _rawId)).Add(Strings.TokenReward);
		}
	}

	/// <summary>
	/// Readout for the "add to deck" button on the end-of-run reward screen, which adds
	/// the revealed reward cards to the player's collection. The button carries no label
	/// of its own (it would otherwise speak its raw object name), so the action word is
	/// authored. The banner above it, the game's own localized text for what is being
	/// added, leads as the distinguishing content; it is dropped when empty.
	/// </summary>
	public sealed class RewardAddElement : UIElement {
		private readonly string _banner;

		public RewardAddElement(string banner) {
			_banner = banner;
		}

		public override Message Describe() {
			return new Message().Add(_banner).Add(Strings.AddToDeck);
		}
	}

	/// <summary>
	/// Readout for a settings slider row (the volume sliders). The slider draws its
	/// value only as a fill sprite with no text, so the generic sweep spoke the row
	/// title alone and an adjustment spoke nothing at all. The value is the slider's
	/// live 0..1 fill, worded as a percentage after the title; a left/right change
	/// re-announces just the percentage.
	/// </summary>
	public sealed class SliderElement : UIElement {
		private readonly string[] _labels;
		private readonly float _value01;

		public SliderElement(string[] labels, float value01) {
			_labels = labels ?? new string[0];
			_value01 = value01;
		}

		public override Message Describe() {
			return new Message().AddRange(_labels).Add(Percent());
		}

		public override Message DescribeValue() {
			return new Message().Add(Percent());
		}

		private string Percent() {
			return Strings.SliderPercent((int)System.Math.Round(_value01 * 100f));
		}
	}

	/// <summary>
	/// Readout for a settings row whose value is a label the game rewrites in place
	/// (the on/off toggles and the resolution/quality/language selectors). The row's
	/// title labels lead, then the value; a left/right change re-announces just the
	/// new value instead of the whole row. The adapter excludes the value label from
	/// the title sweep so the value is never doubled or mistaken for the title.
	/// </summary>
	public sealed class SettingElement : UIElement {
		private readonly string[] _labels;
		private readonly string _value;

		public SettingElement(string[] labels, string value) {
			_labels = labels ?? new string[0];
			_value = value;
		}

		public override Message Describe() {
			return new Message().AddRange(_labels).Add(_value);
		}

		public override Message DescribeValue() {
			return new Message().Add(_value);
		}
	}

	/// <summary>
	/// Readout for a key-binding row on the controls screen. Focus sits on the key
	/// button, whose own label is only the key name, so the generic sweep dropped the
	/// action being bound; the action leads as the distinguishing word, then the key.
	/// A row the game flags invalid (the same key bound to two actions, shown only as
	/// a red tint) appends the conflict word, on both the full readout and the
	/// value-only re-announce after a rebind.
	/// </summary>
	public sealed class BindingElement : UIElement {
		private readonly string _action;
		private readonly string _key;
		private readonly bool _invalid;

		public BindingElement(string action, string key, bool invalid) {
			_action = action;
			_key = key;
			_invalid = invalid;
		}

		public override Message Describe() {
			return AppendConflict(new Message().Add(_action).Add(_key));
		}

		public override Message DescribeValue() {
			return AppendConflict(new Message().Add(_key));
		}

		private Message AppendConflict(Message message) {
			if (_invalid)
				message.Add(Strings.BindingConflict);
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
