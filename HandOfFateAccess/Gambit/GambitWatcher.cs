using HandOfFateAccess.Focus;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Drives the chance gambit end to end so a blind player can play the gamble by ear.
	///
	/// Establish: when the chance cards flip face up, game time is frozen (which holds the
	/// shuffle, since the game's face-up wait is scaled time) and the slots are walked left to
	/// right. Each slot plays its identity tone and THEN speaks its localized outcome, panned to
	/// the slot, so the player hears the sound and learns what it means. Time is then restored.
	///
	/// Shuffle: when the shuffle begins, each card's identity tone sustains as a seamless loop
	/// and pans to follow that card's live position, so the player tracks their chosen card to
	/// where it lands.
	///
	/// Select: once the cards are pickable the gambit goes silent. The player navigates to the
	/// position they tracked through the shuffle and end hold; making any sound per card here
	/// would either reveal the hidden outcome or clutter the pick.
	///
	/// Tones and words play through <see cref="GambitVoice"/> objects (equal-power panning,
	/// like the projectile voices and the validated prototype), not the panStereo audio backend,
	/// so positions read evenly across the field. Each card's identity is its starting slot; it
	/// travels with the card (held by live reference, the sanctioned exception to the
	/// no-cached-state rule) while its type and position are re-read every frame. Sequencing uses
	/// unscaled time. The Harmony hooks only set flags; all work runs from the Update pump. A
	/// frozen-time safety guarantees time is restored so the gambit can never hang the game.
	/// </summary>
	public sealed class GambitWatcher {
		private const int MaxSlots = 9;             // CardChoiceContainer.maxCards
		private const float SlotGapSeconds = 0.15f;
		private const float EstablishSafetySeconds = 12f;
		private const float ShuffleSafetySeconds = 25f;
		private const float ShuffleSettleSeconds = 0.1f;   // hold the final positions before picking
		private const float PanSmoothTau = 0.12f;   // glide each tone toward its new slot pan

		private enum Phase { Idle, Establishing, Shuffling, Selecting }

		private readonly GambitStatusSpeech _speech;
		private bool _available;
		private int _outRate;

		// Identity tone buffers, generated once at the output rate. _toneBuffers are the short
		// Establish/probe one-shots; _sustainBuffers are the seamless shuffle loops.
		private float[][] _toneBuffers;
		private float[][] _sustainBuffers;
		private readonly float[] _toneDurations = new float[MaxSlots];

		// Equal-power voices: one per card for the shuffle, plus a tone and a word voice for the
		// sequential Establish walk.
		private GambitVoice[] _cardVoices;
		private GambitVoice _toneVoice;
		private GambitVoice _wordVoice;

		private Phase _phase;
		// True from establishing a set until its cards clear, so a gambit establishes once even
		// though its cards flip face up again on the final reveal.
		private bool _establishedThisGambit;
		private bool _awaitingShuffle;
		private bool _frozen;
		private float _restoreTimeScale = 1f;

		// The tracked set, captured when the walk begins. The refs are live components (their
		// type and position are re-read each frame, never copied); index doubles as identity.
		private ChanceCard[] _cards;
		private int _count;

		// Establish walk: a two-step-per-slot timeline (play the tone, then speak the word).
		private int _cursor;
		private float _nextEventTime;
		private bool _wordPending;
		private ChanceOutcome _pendingOutcome;
		private float _pendingPan;
		private float _establishDeadline;

		// Shuffle: _voicePan is each tone's current (eased) pan, gliding toward its card's slot.
		private float[] _voicePan;
		private float _shuffleDeadline;
		private bool _settling;        // in the brief end-of-shuffle hold
		private float _settleDeadline;

		public GambitWatcher(GambitStatusSpeech speech) {
			_speech = speech;
		}

		/// <summary>Generates the identity tone buffers and creates the equal-power voices.
		/// Available only if the gambit speech (SAPI) came up, since the tones are useless
		/// without the spoken outcomes that name them.</summary>
		public bool Initialize() {
			if (_toneVoice != null) return _available;   // idempotent: never build a second voice set
			_available = _speech != null && _speech.IsAvailable;
			if (!_available) return false;

			_outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
			_toneBuffers = new float[MaxSlots][];
			_sustainBuffers = new float[MaxSlots][];
			for (int i = 0; i < MaxSlots; i++) {
				_toneBuffers[i] = GambitTones.Generate(i, _outRate);
				_toneDurations[i] = _toneBuffers[i].Length / (float)_outRate;
				_sustainBuffers[i] = GambitTones.GenerateSustain(i, _outRate);
			}

			var root = new GameObject("HoFAccess_GambitVoices");
			UnityEngine.Object.DontDestroyOnLoad(root);
			// A silent looping clip keeps each voice's filter callback firing; the callback
			// overwrites it with the panned buffer.
			AudioClip silence = AudioClip.Create("hofaccess_gambit_silence", 2048, 1, _outRate, false);
			_toneVoice = CreateVoice(root, "GambitToneVoice", silence);
			_wordVoice = CreateVoice(root, "GambitWordVoice", silence);
			_cardVoices = new GambitVoice[MaxSlots];
			for (int i = 0; i < MaxSlots; i++)
				_cardVoices[i] = CreateVoice(root, "GambitCardVoice" + i, silence);

			Log.Info("gambit voices ready");
			return true;
		}

		private static GambitVoice CreateVoice(GameObject root, string name, AudioClip silence) {
			var go = new GameObject(name);
			go.transform.parent = root.transform;
			var voice = go.AddComponent<GambitVoice>();
			voice.Init(silence);
			return voice;
		}

		public void Pump() {
			if (!_available) return;
			try {
				PumpInner();
			} catch (System.Exception ex) {
				// Never let a gambit exception hang the game: restore time, stop voices, and
				// abandon this round. The game's own shuffle still runs, just without our audio.
				Log.Error($"gambit pump failed: {ex}");
				Reset();
			}
		}

		private void PumpInner() {
			DeckManager deckManager = DeckManager.Instance;
			CardChoiceContainer container = deckManager != null ? deckManager.CardChoiceContainer : null;

			// Hard abort (scene change, run teardown): drop everything and restore time.
			if (container == null) {
				if (_phase != Phase.Idle) Reset();
				return;
			}

			switch (_phase) {
				case Phase.Establishing: DriveEstablish(container); return;
				case Phase.Shuffling: DriveShuffle(container); return;
				case Phase.Selecting: DriveSelect(container); return;
			}

			// Idle.
			if (container.Cards.Count == 0) {
				_establishedThisGambit = false;
				_awaitingShuffle = false;
				ChanceFlipSignal.ConsumeFaceUp();
				ChanceShuffleSignal.ConsumeStart();
				return;
			}
			if (_awaitingShuffle && ChanceShuffleSignal.ConsumeStart()) {
				BeginShuffle(container);
				return;
			}
			if (ChanceFlipSignal.ConsumeFaceUp() && !_establishedThisGambit && Capture(container))
				BeginEstablish();
		}

		// Snapshots the cards in slot order. False (so we never freeze) if the choice container
		// holds a non-chance card choice, since FlipCards face-up fires for those too.
		private bool Capture(CardChoiceContainer container) {
			var cards = container.Cards;
			if (cards.Count > MaxSlots) {
				// The container caps at MaxSlots; guard anyway so a larger set degrades to no
				// audio rather than an out-of-range crash on the per-slot voice arrays.
				Log.Warn($"gambit has {cards.Count} cards, more than {MaxSlots}; skipping gambit audio");
				return false;
			}
			var refs = new ChanceCard[cards.Count];
			for (int i = 0; i < cards.Count; i++) {
				ChanceCard card = cards[i] as ChanceCard;
				if (card == null) return false;
				refs[i] = card;
			}
			_cards = refs;
			_count = cards.Count;
			_voicePan = new float[_count];
			return true;
		}

		// --- Establish ---

		private void BeginEstablish() {
			_speech.Prepare();   // render the localized words so their buffers/durations are known
			_establishedThisGambit = true;
			_phase = Phase.Establishing;
			Freeze();
			_cursor = 0;
			_wordPending = false;
			_nextEventTime = Time.unscaledTime;
			_establishDeadline = Time.unscaledTime + EstablishSafetySeconds;
			Log.Info($"gambit establish: {_count} cards, time held");
		}

		// Each slot is two events: play the identity tone, then after it finishes speak the
		// outcome. Sequencing them (rather than together) keeps the tone from masking the word,
		// so the player hears the sound clearly and then learns what it means.
		private void DriveEstablish(CardChoiceContainer container) {
			if (Time.unscaledTime >= _establishDeadline) {
				EndEstablish("safety deadline");
				return;
			}
			if (Time.unscaledTime < _nextEventTime) return;

			if (_wordPending) {
				if (_speech.TryGetWord(_pendingOutcome, out float[] pcm, out int rate))
					_wordVoice.Play(pcm, rate, false, _pendingPan, 1f);
				float wordSeconds;
				if (!_speech.TryGetDuration(_pendingOutcome, out wordSeconds)) wordSeconds = 0.4f;
				_nextEventTime = Time.unscaledTime + wordSeconds + SlotGapSeconds;
				_wordPending = false;
				_cursor++;
				return;
			}
			if (_cursor < _count) {
				StartSlotTone(_cursor);
				return;
			}
			EndEstablish(null);
		}

		private void StartSlotTone(int index) {
			float pan = GambitLayout.SlotPan(index, _count);
			_toneVoice.Play(_toneBuffers[index], _outRate, false, pan, 1f);
			if (_cards[index] != null) {
				_pendingOutcome = ToOutcome(_cards[index].ChanceType);
			} else {
				// A card vanished mid-walk (unexpected while time is frozen): play the tone but
				// log rather than silently speaking a possibly-wrong outcome.
				Log.Warn($"gambit establish: card at slot {index} is gone; outcome unknown");
				_pendingOutcome = ChanceOutcome.Success;
			}
			_pendingPan = pan;
			_nextEventTime = Time.unscaledTime + _toneDurations[index];   // speak after the tone
			_wordPending = true;
		}

		private void EndEstablish(string reason) {
			Unfreeze();
			_phase = Phase.Idle;
			_awaitingShuffle = true;   // the game now flips down and shuffles
			if (reason != null)
				Log.Warn($"gambit establish ended early ({reason}); time restored");
			else
				Log.Info("gambit establish complete; time restored, shuffle released");
		}

		// --- Shuffle ---

		private void BeginShuffle(CardChoiceContainer container) {
			_awaitingShuffle = false;
			_phase = Phase.Shuffling;
			_settling = false;
			_shuffleDeadline = Time.unscaledTime + ShuffleSafetySeconds;

			var cards = container.Cards;
			for (int i = 0; i < _count; i++) {
				if (_cards[i] == null) continue;
				float pan = SlotPan(cards, _cards[i], i);
				_voicePan[i] = pan;
				_cardVoices[i].Play(_sustainBuffers[i], _outRate, true, pan, 1f);
			}
			Log.Info("gambit shuffle: following cards");
		}

		private void DriveShuffle(CardChoiceContainer container) {
			if (container.Cards.Count == 0 || Time.unscaledTime >= _shuffleDeadline) {
				EndShuffle();
				return;
			}

			// Pan each tone toward its card's current slot index, gliding so the swaps read as
			// motion rather than jumps. Index, not world position: the intermediate shuffle
			// layout can pile the cards on top of each other, which would collapse the pan.
			var cards = container.Cards;
			float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / PanSmoothTau);
			for (int i = 0; i < _count; i++) {
				if (_cards[i] == null) continue;
				float target = SlotPan(cards, _cards[i], i);
				_voicePan[i] += (target - _voicePan[i]) * k;
				_cardVoices[i].SetPan(_voicePan[i]);
			}

			// The cards become pickable when the shuffle ends; the first focus on one is the cue.
			// Hold the tones at their settled positions for a brief beat so the landing spot
			// registers before they cut and picking begins.
			if (!_settling) {
				if (FocusedIndex() >= 0) {
					_settling = true;
					_settleDeadline = Time.unscaledTime + ShuffleSettleSeconds;
				}
			} else if (Time.unscaledTime >= _settleDeadline) {
				EndShuffle();
			}
		}

		private void EndShuffle() {
			StopVoices();
			_phase = Phase.Selecting;
			// The card the game auto-selected at shuffle end did not change, so the focus path
			// won't re-read it on its own; force it so its slot number is spoken at once rather
			// than only after the player first moves.
			if (FocusedIndex() >= 0)
				FocusTracker.Refresh(true);
			Log.Info("gambit shuffle ended; cards pickable");
		}

		// --- Select ---

		// Silent while the player picks: they navigate to the position they tracked. End the
		// gambit when the chosen card clears the set.
		private void DriveSelect(CardChoiceContainer container) {
			if (container.Cards.Count == 0)
				Reset();
		}

		// --- Shared ---

		/// <summary>
		/// While picking, the focused chance card's current left-to-right slot number (1-based),
		/// so the focus readout can speak "Slot N" in place of the face-down identity. False
		/// outside the pick or for anything that is not one of this gambit's cards. Position is
		/// the card's live index in the container, not its starting slot, so it reflects where
		/// the card actually landed.
		/// </summary>
		public bool TrySlotName(GameObject focused, out int slotNumber) {
			slotNumber = 0;
			if (!_available || _phase != Phase.Selecting || focused == null) return false;
			Card card = focused.GetComponentInParent<Card>();
			if (card == null) return false;

			bool tracked = false;
			for (int i = 0; i < _count; i++)
				if (_cards[i] == card) { tracked = true; break; }
			if (!tracked) return false;

			DeckManager deckManager = DeckManager.Instance;
			CardChoiceContainer container = deckManager != null ? deckManager.CardChoiceContainer : null;
			if (container == null) return false;
			int index = container.Cards.IndexOf(card);
			if (index < 0) return false;

			slotNumber = index + 1;
			return true;
		}

		// The tracked card currently focused, or -1. Used to detect the end of the shuffle.
		private int FocusedIndex() {
			GameObject selected = UICamera.selectedObject;
			if (selected == null) return -1;
			Card card = selected.GetComponentInParent<Card>();
			if (card == null) return -1;
			for (int i = 0; i < _count; i++)
				if (_cards[i] == card) return i;
			return -1;
		}

		// Pan for a card by its current slot in the container (its position in the layout row),
		// falling back to its identity slot if it is momentarily not found.
		private float SlotPan(System.Collections.Generic.List<Card> cards, Card card, int fallbackIndex) {
			int index = cards.IndexOf(card);
			return GambitLayout.SlotPan(index >= 0 ? index : fallbackIndex, _count);
		}

		private void StopVoices() {
			if (_toneVoice != null) _toneVoice.Stop();
			if (_wordVoice != null) _wordVoice.Stop();
			if (_cardVoices == null) return;
			for (int i = 0; i < _cardVoices.Length; i++)
				if (_cardVoices[i] != null) _cardVoices[i].Stop();
		}

		private void Freeze() {
			_restoreTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
			Time.timeScale = 0f;
			_frozen = true;
		}

		private void Unfreeze() {
			if (!_frozen) return;
			Time.timeScale = _restoreTimeScale;
			_frozen = false;
		}

		private void Reset() {
			StopVoices();
			Unfreeze();
			_phase = Phase.Idle;
			_establishedThisGambit = false;
			_awaitingShuffle = false;
			ChanceFlipSignal.ConsumeFaceUp();
			ChanceShuffleSignal.ConsumeStart();
		}

		private static ChanceOutcome ToOutcome(ChanceType type) {
			switch (type) {
				case ChanceType.Success: return ChanceOutcome.Success;
				case ChanceType.HugeSuccess: return ChanceOutcome.HugeSuccess;
				case ChanceType.Failure: return ChanceOutcome.Failure;
				case ChanceType.HugeFailure: return ChanceOutcome.HugeFailure;
				default: return ChanceOutcome.Success;
			}
		}
	}
}
