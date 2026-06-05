using System;
using System.Collections.Generic;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Speech;
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
				EncounterEventReader.Read(out var narrative, out var instructions, out var cardDescription);
				text = EncounterNarration.Compose(narrative, instructions, cardDescription);
			} catch (Exception ex) {
				Log.Error("encounter text readout failed: " + ex);
				return;
			}
			if (text == _lastEncounterText) return;
			_lastEncounterText = text;
			if (!string.IsNullOrEmpty(text))
				Speak(text);
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
