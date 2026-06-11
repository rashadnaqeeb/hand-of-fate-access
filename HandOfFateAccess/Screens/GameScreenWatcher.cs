using System;
using System.Collections.Generic;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Maps;
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
		// The Game instance the delegates are attached to. The reset-progress scene wipe
		// destroys it and card_table reload creates a fresh one with empty delegates, so
		// the pump compares by reference and resubscribes when the instance changes.
		private Game _game;

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
		// The narrative and instructions last announced from the encounter panel, tracked
		// separately so an instructions line appended under an unchanged narrative is spoken
		// alone instead of re-reading the narrative (see EncounterNarration.Decide).
		private string _lastEncounterNarrative;
		private string _lastEncounterInstructions;
		// Same edge markers for the other display-only surfaces the focus model never
		// reaches: the cabinet examine panel and the death/forfeit results line. The cabinet
		// also tracks the examined card's name separately, so it is announced once when the
		// examined card changes rather than repeated on every section page.
		private string _lastCabinetText;
		private string _lastCabinetCardName;
		// The Fates pile's info panel, the same display-only surface as the cabinet: the
		// archetype name (announced when the selected archetype changes) and the active
		// section (announced when it changes, by selection or by paging). The panel rebuilds
		// over a frame or two, so the read is debounced: pending holds the last read, spoken
		// holds what was last announced once the read settled.
		private string _archetypePendingText;
		private string _archetypePendingName;
		private string _archetypeSpokenText;
		private string _archetypeSpokenName;
		private string _lastDeathText;
		private string _lastScoreboardText;
		private string _lastSubtitleText;
		private string _lastZoomText;
		private string _lastNavActions;
		// How many leading combat-roster cards have been announced; advanced by
		// MonsterNarration.RosterStep as each dealt card is spoken.
		private int _monsterRosterSpokenCount;

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
			_game = Game.Instance;
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
			// A reset wiped the Game instance out from under the subscription (its
			// OnDestroy nulls the static, the reloaded scene's Awake reassigns it), so
			// the delegates this watcher attached are gone with the old instance.
			// ReferenceEquals, never Unity's destroyed-equals-null ==, so the dead
			// window between wipe and reload also unsubscribes and Install retries.
			if (_subscribed && !ReferenceEquals(Game.Instance, _game)) {
				_subscribed = false;
				_game = null;
				_waitingLogged = false;
				Log.Info("screen watcher lost Game instance (scene reset); resubscribing");
			}
			if (!_subscribed) {
				Install();
				return;
			}
			PumpOverlay(Encounter.Instance != null, ref _encounterActive, ScreenId.Encounter);
			PumpOverlay(CombatEncounter.Instance != null, ref _combatActive, ScreenId.Combat);
			PumpOverlay(Shop.Instance != null, ref _shopActive, ScreenId.Shop);
			PumpOverlay(PauseMenuOpen(), ref _pauseActive, ScreenId.Paused);
			PumpPauseState();
			PumpDialogue();
			PumpEncounterText();
			PumpMonsterRoster();
			PumpCabinetText();
			PumpArchetypeText();
			PumpDeathText();
			PumpScoreboardText();
			PumpSubtitleText();
			PumpZoomText();
			PumpRewardReveal();
			PumpMapReveal();
			PumpNavActions();
		}

		// The death line and the scoreboard are both display-only surfaces on the results
		// states; their per-frame FindObjectOfType is scoped here so it only runs there.
		private static bool InResults() {
			GameState state = Game.Instance != null ? Game.Instance.ActiveGameState : null;
			return state is GameState_Post || state is GameState_AlphaEnd;
		}

		// The encounter event panel's narrative (scenario, then result after a choice) and
		// its mechanical instructions are display-only text the focus model never reaches.
		// Edge-detect a change and announce it. Routed through Speak so it interrupts as the
		// new context and the Continue button the game then selects queues behind it, the
		// same ordering as a dialogue. The narrative and instructions are tracked separately
		// so an instructions line appended under an unchanged narrative is not re-read as the
		// whole line; the blank flashed mid-transition is held, not reset (see
		// EncounterNarration.Decide); the markers re-arm only when the panel stops showing.
		private void PumpEncounterText() {
			EncounterAnnouncement result;
			try {
				EncounterEventReader.Read(out var showing, out var narrative, out var instructions);
				result = EncounterNarration.Decide(showing, narrative, instructions,
					_lastEncounterNarrative, _lastEncounterInstructions);
			} catch (Exception ex) {
				Log.Error("encounter text readout failed: " + ex);
				return;
			}
			_lastEncounterNarrative = result.MarkerNarrative;
			_lastEncounterInstructions = result.MarkerInstructions;
			if (result.Speak)
				Speak(result.Text);
		}

		// The monster cards dealt for a combat encounter are laid on the table, then at
		// level-in animate off into the 3D arena; nothing ever focuses them, so the roster
		// is read here while it is on the table. The dealer deals one card at a time and
		// awaits its flight animation before the next, so each card is announced once as
		// it lands (the unspoken tail per RosterStep) rather than recomposing the whole
		// line-up, which would re-read the earlier cards. A title is complete the moment
		// the card enters the container (read off the card model, not its labels). Queued
		// so it follows the scenario the event panel announces rather than cutting it off.
		private void PumpMonsterRoster() {
			IList<string> titles;
			try {
				titles = MonsterRosterReader.Read();
			} catch (Exception ex) {
				Log.Error("monster roster readout failed: " + ex);
				return;
			}
			string text = MonsterNarration.RosterStep(titles, ref _monsterRosterSpokenCount);
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// The cabinet examine panel (lore, deck changes, upgrades) is display-only text
		// reached from the live StartOptions singleton. The court card the player examined
		// stays focused and speaks its own name, so this section detail queues behind that
		// name rather than interrupting it, and each bumper page to a new section re-reads.
		// Empty text (panel closed) just clears the edge marker.
		private void PumpCabinetText() {
			string text;
			string cardName;
			try {
				CabinetReader.Read(out var name, out var section, out var body);
				text = CabinetNarration.Compose(section, body);
				cardName = new Message().Add(name).Resolve();
			} catch (Exception ex) {
				Log.Error("cabinet text readout failed: " + ex);
				return;
			}
			// Announce the examined card's name once, when the examined card changes. The
			// examine banner shows it (even for a card face-down on the rack), and the
			// per-section readout never includes it, so it would otherwise be dropped. Queued
			// ahead of the section text, which changes in the same frame on a fresh examine.
			if (cardName != _lastCabinetCardName) {
				_lastCabinetCardName = cardName;
				if (!string.IsNullOrEmpty(cardName))
					SpeechPipeline.SpeakQueued(cardName);
			}
			if (text == _lastCabinetText) return;
			_lastCabinetText = text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// The Fates pile's archetype info panel is the same display-only CabinetCardInfo the
		// cabinet uses, but the focused archetype card is suppressed, so this is the sole
		// announcer. The panel tears down and rebuilds its card list over a frame or two when
		// a new archetype is selected, so the read is debounced: only once it stops changing
		// is it announced, which keeps a settling panel from being read (and re-cut) repeatedly.
		// The name interrupts as the navigation feedback; the section queues behind it so it is
		// never cut off. Empty (outside the Fates pile) settles to empty and clears the markers,
		// so re-entry re-announces.
		private void PumpArchetypeText() {
			string name;
			string text;
			try {
				ArchetypeReader.Read(out var rawName, out var locked, out var section, out var body);
				name = ArchetypeNarration.ComposeName(rawName, locked);
				text = ArchetypeNarration.ComposeSection(section, body);
			} catch (Exception ex) {
				Log.Error("archetype text readout failed: " + ex);
				return;
			}
			// Still changing this frame: hold it and wait for the rebuild to settle.
			if (name != _archetypePendingName || text != _archetypePendingText) {
				_archetypePendingName = name;
				_archetypePendingText = text;
				return;
			}
			bool nameChanged = name != _archetypeSpokenName;
			bool textChanged = text != _archetypeSpokenText;
			if (!nameChanged && !textChanged) return;
			_archetypeSpokenName = name;
			_archetypeSpokenText = text;
			if (nameChanged && !string.IsNullOrEmpty(name))
				SpeechPipeline.SpeakInterrupt(name);
			if (textChanged && !string.IsNullOrEmpty(text))
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

		// A card zoom presents a single card for a decision (examine, equip, buy, keep,
		// reveal) on a locked sole selection with display-only detail panels, so the focus
		// path cannot read it. The ZoomReader reads the whole zoom off the model; announce
		// the decision line interrupting (it is the new context), then queue the button-
		// label hint behind it so acting quickly skips it. Empty (no zoom) clears the
		// marker. Focus is suppressed for zoom cards in ProxyFactory, so this is the sole
		// announcer and does not double-speak.
		private void PumpZoomText() {
			ZoomAnnouncement announcement;
			try {
				announcement = ZoomNarration.Compose(ZoomReader.Read());
			} catch (Exception ex) {
				Log.Error("zoom readout failed: " + ex);
				return;
			}
			if (announcement.Main == _lastZoomText) return;
			_lastZoomText = announcement.Main;
			if (string.IsNullOrEmpty(announcement.Main)) return;
			SpeechPipeline.SpeakInterrupt(announcement.Main);
			if (!string.IsNullOrEmpty(announcement.Hint))
				SpeechPipeline.SpeakQueued(announcement.Hint);
		}

		// The nav bar's context-action buttons (the game's Function0/Function1: the scoreboard's
		// high-score toggle and restart, the deck builder's fill-deck) are mapped to controller
		// face buttons and function keys, outside the focusable selection, so the focus path
		// never reaches them. Edge-detect their labels and announce when they change, so the
		// available actions are discoverable. Queued so they follow the screen and focus rather
		// than cutting them off. Empty (no such actions on this screen) clears the marker, so
		// re-entering a screen that has them announces afresh.
		private void PumpNavActions() {
			string text;
			try {
				text = NavActionsNarration.Compose(NavBarReader.ReadActions());
			} catch (Exception ex) {
				Log.Error("nav actions readout failed: " + ex);
				return;
			}
			if (text == _lastNavActions) return;
			_lastNavActions = text;
			if (!string.IsNullOrEmpty(text))
				SpeechPipeline.SpeakQueued(text);
		}

		// A reward card flipped face-up on the end-of-run reward screen is read here: the
		// game moves focus straight to the next card when one is clicked, so the focus path
		// never reads the revealed reward. The OnCardClicked hook records the flipped card;
		// this reads its identity off the model the same way the focus path reads a card.
		// Routed through Speak so it interrupts as the reveal and the following focus (the
		// next face-down card, or the add button) queues behind it rather than cutting it off.
		private void PumpRewardReveal() {
			if (!RewardReveal.TryConsume(out Card card)) return;
			string text;
			try {
				text = new CardElement(ProxyFactory.ExtractCard(card)).Describe().Resolve();
			} catch (Exception ex) {
				Log.Error("reward reveal readout failed: " + ex);
				return;
			}
			Speak(text);
		}

		// Map cards flipped face-up by a reveal effect (Explorer's Helmet, an encounter's
		// reveal reward...) are read here: the game locks selection to a bare confirm
		// button while the camera flies to the flipping cards, so the focus path never
		// reads what was revealed. The Show hook records the live slot list; TryConsume
		// holds it back until every card's Revealed flag is set (they flip one at a time
		// over several frames), then each slot is read off the model, now face-up. Routed
		// through Speak so the reveal interrupts as the new context and the confirm
		// prompt queues behind it.
		private void PumpMapReveal() {
			string text;
			try {
				if (!MapReveal.TryConsume(out var slots)) return;
				var infos = new List<MapSlotInfo>();
				foreach (MapLayoutSlot slot in slots) {
					MapSlotInfo info = MapSlotReader.Read(slot);
					if (info != null)
						infos.Add(info);
				}
				text = MapRevealReadout.Compose(infos);
			} catch (Exception ex) {
				MapReveal.Clear();
				Log.Error("map reveal readout failed: " + ex);
				return;
			}
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

		// Pause sub-screens that carry display-only content the focus path never
		// reaches, edge-detected on the pause state name. The reset panel parks focus
		// on its yes/no buttons while its header and warning are bare labels; the
		// panel renders exactly the two game strings read here by key (no component
		// exposes the labels). The credits screen has no focusable content at all; its
		// data lives on ScriptableObjects read once the panel is live (it enables a
		// frame or two after the state flips, hence the pending retry).
		private PauseMenuManager.State.StateName _lastPauseStateName = PauseMenuManager.State.StateName.None;
		private bool _creditsPending;

		private void PumpPauseState() {
			PauseMenuManager.State.StateName name = CurrentPauseStateName();
			if (name != _lastPauseStateName) {
				_lastPauseStateName = name;
				_creditsPending = name == PauseMenuManager.State.StateName.Credits;
				if (name == PauseMenuManager.State.StateName.ResetProfile)
					Speak(new Message()
						.Add(UIUtils.GetString("MENU_SETTINGS_RESET_PROFILE"))
						.Add(UIUtils.GetString("MENU_SETTINGS_RESET_PROFILE_PROMPT"))
						.Resolve());
			}

			if (!_creditsPending) return;
			// The rail selection that opened the screen is usually still pending this
			// frame (the menu opens on selection, in the same input dispatch), and its
			// announcement would interrupt away anything queued before it. Let it
			// speak first; the credits then queue behind it.
			if (FocusTracker.HasPending) return;
			IList<CreditsSection> sections;
			try {
				sections = CreditsReader.Read();
			} catch (Exception ex) {
				Log.Error("credits readout failed: " + ex);
				_creditsPending = false;
				return;
			}
			if (sections == null) return; // panel not live yet; retry next frame
			_creditsPending = false;
			// One queued line per section, so leaving the screen (any interrupting
			// announcement) cuts the rest off naturally.
			foreach (CreditsSection section in sections) {
				string line = CreditsNarration.ComposeSection(section);
				if (!string.IsNullOrEmpty(line))
					SpeechPipeline.SpeakQueued(line);
			}
		}

		private static PauseMenuManager.State.StateName CurrentPauseStateName() {
			MenuManager menus = MenuManager.Instance;
			if (menus == null) return PauseMenuManager.State.StateName.None;
			PauseMenuManager pause = menus.PauseMenuManager;
			if (pause == null || pause.CurrentState == null) return PauseMenuManager.State.StateName.None;
			return pause.CurrentState.Name;
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
