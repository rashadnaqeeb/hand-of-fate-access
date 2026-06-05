using System;
using System.Collections.Generic;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Speech;
using HandOfFateAccess.UI;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Tracks which game context the player is in and announces it on entry, driven
	/// by Hand of Fate's own GameState machine rather than polling for it. Top-level
	/// transitions (menu -> deck builder -> map -> level) come from the game's
	/// OnGameStateEntered delegate; the GameState subclass is mapped to a mod
	/// <see cref="ScreenId"/> through a compile-time typeof table, so a game-side
	/// rename breaks the build instead of silently mis-mapping.
	///
	/// Sub-contexts inside level play (encounter, combat, shop) have no entry
	/// delegate we subscribe to, so they are detected by edge-polling their live
	/// singletons from the per-frame pump. Both feed the same <see cref="ScreenStack"/>,
	/// which decides what (if anything) is spoken.
	///
	/// The stack is exposed for Phase 4 input dispatch and Phase 5 focus override to
	/// read; this increment only announces.
	/// </summary>
	internal sealed class GameScreenWatcher {
		private readonly ScreenStack _stack = new ScreenStack();
		private bool _subscribed;
		private bool _waitingLogged;
		private bool _screenJustChanged;

		// Overlay presence last observed, so the pump acts only on the live<->null edge.
		private bool _encounterActive;
		private bool _combatActive;
		private bool _shopActive;
		private bool _pauseActive;
		// The dialogue's identity, not just present/absent, so a second dialogue
		// stacked on the first (and the first revealed when it closes) is detected.
		private Dialogue _topDialogue;
		// The encounter narrative last announced, for edge detection only: the live text
		// is always re-read and spoken, this just marks the scenario->result change.
		private string _lastEncounterText;
		// Same edge markers for the other display-only surfaces the focus model never
		// reaches: the cabinet examine panel and the death/forfeit results line.
		private string _lastCabinetText;
		private string _lastDeathText;
		private string _lastScoreboardText;
		private string _lastSubtitleText;
		private string _lastCompareText;

		// Live game types mapped to mod screens. Compile-time references against
		// Assembly-CSharp: a renamed GameState fails the build here, by design.
		private static readonly Dictionary<Type, ScreenId> StateMap = new Dictionary<Type, ScreenId> {
			{ typeof(GameState_Init),         ScreenId.Loading },
			{ typeof(GameState_Splash),       ScreenId.Loading },
			{ typeof(GameState_AlphaSplash),  ScreenId.Loading },
			{ typeof(GameState_Intro),        ScreenId.Intro },
			{ typeof(GameState_Attract),      ScreenId.Attract },
			{ typeof(GameState_StartScreen),  ScreenId.MainMenu },
			{ typeof(GameState_DeckBuilder),  ScreenId.DeckBuilder },
			{ typeof(GameState_DungeonSelect), ScreenId.DungeonSelect },
			{ typeof(GameState_Map),          ScreenId.Map },
			{ typeof(GameState_Level_In),     ScreenId.Loading },
			{ typeof(GameState_Level_Play),   ScreenId.CardTable },
			{ typeof(GameState_Level_Out),    ScreenId.Loading },
			// Pause is an overlay, not a base screen: the card table is still
			// underneath. The game flips ActiveGameState to Level_Pause only when
			// pausing from level play, so mapping it to the underlying CardTable
			// keeps that incidental flip from double-announcing the pause overlay.
			{ typeof(GameState_Level_Pause),  ScreenId.CardTable },
			{ typeof(GameState_Inventory),    ScreenId.Inventory },
			{ typeof(GameState_Post),         ScreenId.Results },
			{ typeof(GameState_AlphaEnd),     ScreenId.Results },
			{ typeof(GameState_Cabinet),      ScreenId.Cabinet },
		};

		/// <summary>The active screen context, read-only for input/focus consumers.</summary>
		public ScreenStack Stack => _stack;

		/// <summary>
		/// True exactly once after a screen entry was spoken, so the focus pump can
		/// speak the immediately-following control announcement queued (after the
		/// screen name) instead of interrupting it. Consuming clears the flag.
		/// </summary>
		public bool ConsumeScreenJustChanged() {
			bool v = _screenJustChanged;
			_screenJustChanged = false;
			return v;
		}

		/// <summary>
		/// Subscribe to the game's state delegates and seed the base from the current
		/// state. If Game.Instance is not live yet, this is a no-op; Pump retries.
		/// </summary>
		public void Install() {
			if (_subscribed) return;
			if (Game.Instance == null) {
				if (!_waitingLogged) {
					Log.Info("screen watcher waiting for Game.Instance");
					_waitingLogged = true;
				}
				return;
			}

			Game.Instance.OnGameStateEntered += OnGameStateEntered;
			Game.Instance.OnGameStateExited += OnGameStateExited;
			_subscribed = true;
			Log.Info("screen watcher installed");

			if (Game.Instance.ActiveGameState != null)
				Speak(_stack.SetBase(MapState(Game.Instance.ActiveGameState)));
		}

		/// <summary>
		/// Per-frame work: finish a deferred subscription, then edge-detect the
		/// encounter/combat/shop sub-contexts and announce overlay changes.
		/// </summary>
		public void Pump() {
			if (!_subscribed) {
				Install();
				return;
			}
			PumpOverlay(Encounter.Instance != null, ref _encounterActive, ScreenId.Encounter);
			PumpOverlay(CombatEncounter.Instance != null, ref _combatActive, ScreenId.Combat);
			PumpOverlay(Shop.Instance != null, ref _shopActive, ScreenId.Shop);
			PumpOverlay(PauseMenuOpen(), ref _pauseActive, ScreenId.Paused);
			PumpDialogue();
			PumpEncounterText();
			PumpCabinetText();
			PumpDeathText();
			PumpScoreboardText();
			PumpSubtitleText();
			PumpCompareText();
		}

		// The death line and the scoreboard are both display-only surfaces on the results
		// states; their per-frame FindObjectOfType is scoped here so it only runs there.
		private static bool InResults() {
			GameState state = Game.Instance != null ? Game.Instance.ActiveGameState : null;
			return state is GameState_Post || state is GameState_AlphaEnd;
		}

		// The encounter event panel's narrative (scenario, then result after a choice)
		// is display-only text the focus model never reaches. Edge-detect a change in
		// the live text and announce it. Routed through Speak so it interrupts as the
		// new context and the Continue button the game then selects queues behind it,
		// the same ordering as a dialogue. Empty text (panel reset/closed) just clears
		// the edge marker.
		private void PumpEncounterText() {
			string text;
			try {
				EncounterEventReader.Read(out var narrative, out var instructions);
				text = EncounterNarration.Compose(narrative, instructions);
			} catch (Exception ex) {
				Log.Error("encounter text readout failed: " + ex);
				return;
			}
			if (text == _lastEncounterText) return;
			_lastEncounterText = text;
			if (!string.IsNullOrEmpty(text))
				Speak(text);
		}

		// The cabinet examine panel (lore, deck changes, upgrades) is display-only text
		// reached from the live StartOptions singleton. The court card the player examined
		// stays focused and speaks its own name, so this section detail queues behind that
		// name rather than interrupting it, and each bumper page to a new section re-reads.
		// Empty text (panel closed) just clears the edge marker.
		private void PumpCabinetText() {
			string text;
			try {
				CabinetReader.Read(out var section, out var body);
				text = CabinetNarration.Compose(section, body);
			} catch (Exception ex) {
				Log.Error("cabinet text readout failed: " + ex);
				return;
			}
			if (text == _lastCabinetText) return;
			_lastCabinetText = text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// The death/forfeit results line is display-only text UIDeathPanel sets on the
		// results screen. Gated to the results states so the per-frame FindObjectOfType
		// only runs there. Interrupts as the reveal of the run's outcome.
		private void PumpDeathText() {
			if (!InResults()) {
				_lastDeathText = null;
				return;
			}
			string text;
			try {
				text = new Message().Add(DeathPanelReader.Read()).Resolve();
			} catch (Exception ex) {
				Log.Error("death text readout failed: " + ex);
				return;
			}
			if (text == _lastDeathText) return;
			_lastDeathText = text;
			if (!string.IsNullOrEmpty(text))
				Speak(text);
		}

		// The end-of-run score breakdown is display-only; only its continue button is
		// focusable. Gated to the results states. Queued so it reads after the death line
		// (which interrupts as the outcome reveal) rather than cutting it off.
		private void PumpScoreboardText() {
			if (!InResults()) {
				_lastScoreboardText = null;
				return;
			}
			string text;
			try {
				ScoreboardReader.Read(out var header, out var rows);
				text = ScoreboardNarration.Compose(header, rows);
			} catch (Exception ex) {
				Log.Error("scoreboard text readout failed: " + ex);
				return;
			}
			if (text == _lastScoreboardText) return;
			_lastScoreboardText = text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// The dealer's subtitle line is display-only narration the focus model never
		// reaches. Each new segment queues behind current speech so it does not cut off
		// navigation feedback. The game populates this only when the player has subtitles
		// enabled, so an empty read (subtitles off, or no line playing) simply says
		// nothing, respecting that setting.
		//
		// Hide() does not clear the label, so the last line lingers while the widget is
		// hidden. Reset the marker whenever it is hidden, so a line that plays again
		// (identical text) is seen as a fresh edge and re-announced rather than skipped.
		private void PumpSubtitleText() {
			string text;
			bool visible;
			try {
				SubtitleReader.Read(out var raw, out visible);
				text = new Message().Add(raw).Resolve();
			} catch (Exception ex) {
				Log.Error("subtitle text readout failed: " + ex);
				return;
			}
			if (!visible) {
				_lastSubtitleText = null;
				return;
			}
			if (text == _lastSubtitleText) return;
			_lastSubtitleText = text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// The equipment-replace prompt compares new gear against what it would replace on
		// display-only panels, with a confirm/cancel that has no navigable buttons, so the
		// whole decision is otherwise silent. Announce the comparison interrupting (it is
		// the new decision context), then queue the action hint behind it so acting quickly
		// skips it. Empty (prompt closed) just clears the edge marker.
		private void PumpCompareText() {
			string text;
			try {
				CompareReader.Read(out var newItem, out var oldItem);
				text = CompareNarration.Compose(newItem, oldItem);
			} catch (Exception ex) {
				Log.Error("equipment compare readout failed: " + ex);
				return;
			}
			if (text == _lastCompareText) return;
			_lastCompareText = text;
			if (string.IsNullOrEmpty(text)) return;
			SpeechPipeline.SpeakInterrupt(text);
			SpeechPipeline.SpeakQueued(Strings.EquipReplaceHint);
		}

		// A modal dialogue is a MenuManager push on the Dialogue stack, invisible to
		// the GameState machine. Unlike the other overlays it speaks its own prompt
		// text (the question), not the generic catalog name, since that is the
		// information focus does not give -- focus reads the buttons. We track the top
		// dialogue's identity (ReferenceEquals, never Unity's destroyed-equals-null ==)
		// so a dialogue stacked on another, or one revealed when the top closes, is
		// re-read instead of staying silent. The single Dialogue overlay on the stack
		// stands for "a modal is up"; only the spoken prompt changes between them. On
		// the last one closing, the revealed screen is re-announced via the pop path.
		private void PumpDialogue() {
			Dialogue dialogue = CurrentDialogue();
			if (ReferenceEquals(dialogue, _topDialogue)) return;
			bool wasPresent = !ReferenceEquals(_topDialogue, null);
			_topDialogue = dialogue;

			if (dialogue == null) {
				Speak(_stack.PopOverlay(ScreenId.Dialogue));
				return;
			}

			if (!wasPresent)
				_stack.PushOverlay(ScreenId.Dialogue);

			string prompt;
			try {
				prompt = DialogueReader.Read(dialogue).Compose();
			} catch (Exception ex) {
				// Never leave a modal silent: announce the generic name, log the read
				// failure so it is actionable rather than invisible.
				Log.Error("dialogue readout failed: " + ex);
				prompt = ScreenCatalog.NameOf(ScreenId.Dialogue);
			}
			Speak(prompt);
		}

		private static Dialogue CurrentDialogue() {
			MenuManager menus = MenuManager.Instance;
			if (menus == null) return null;
			return menus.CurrentMenu(MenuManager.StackPriority.Dialogue) as Dialogue;
		}

		// The pause menu is a MenuManager push, not a GameState change, so it is
		// detected here rather than through the state delegates. Any non-None pause
		// state (including the controls/audio sub-screens) counts as open; Init is
		// the transient seed state before the menu settles to None. The accessors
		// are legitimately null early in load.
		private static bool PauseMenuOpen() {
			MenuManager menus = MenuManager.Instance;
			if (menus == null) return false;
			PauseMenuManager pause = menus.PauseMenuManager;
			if (pause == null || pause.CurrentState == null) return false;
			PauseMenuManager.State.StateName name = pause.CurrentState.Name;
			return name != PauseMenuManager.State.StateName.None
				&& name != PauseMenuManager.State.StateName.Init;
		}

		private void PumpOverlay(bool live, ref bool active, ScreenId id) {
			if (live == active) return;
			active = live;
			Speak(live ? _stack.PushOverlay(id) : _stack.PopOverlay(id));
		}

		private void OnGameStateEntered(GameState state) {
			Speak(_stack.SetBase(MapState(state)));
		}

		private void OnGameStateExited(GameState state) {
			Log.Debug("game state exited: " + state.GetType().Name);
		}

		private static ScreenId MapState(GameState state) {
			ScreenId id;
			if (StateMap.TryGetValue(state.GetType(), out id))
				return id;
			Log.Warn("unmapped game state: " + state.GetType().Name);
			return ScreenId.Unknown;
		}

		// Screen entry supersedes the old context, so it interrupts; the flag tells
		// the focus pump to queue the control announcement that follows. Gate it on a
		// focus actually being pending: a screen with no following focus (the
		// free-roam map, or a pop that reveals an already-focused control) must not
		// leave the flag latched to mis-queue a later unrelated navigation.
		private void Speak(string name) {
			if (string.IsNullOrEmpty(name)) return;
			SpeechPipeline.SpeakInterrupt(name);
			_screenJustChanged = FocusTracker.HasPending;
		}
	}
}
