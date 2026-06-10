using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Gambit;
using HandOfFateAccess.Input;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Maps;
using HandOfFateAccess.Patches;
using HandOfFateAccess.Patching;
using HandOfFateAccess.Resources;
using HandOfFateAccess.Screens;
using HandOfFateAccess.Speech;
using HandOfFateAccess.UI;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess {
	/// <summary>
	/// BepInEx entry point. Awake does only non-Unity setup (logging); everything
	/// else is deferred to the Update loop. Awake runs inside the BepInEx
	/// chainloader during Application's static constructor, before the engine
	/// main loop is running: touching Unity APIs there crashes, and initializing
	/// the screen-reader bridge there appears to interfere with the game's
	/// startup. Native preload, Tolk init, Harmony patching, and the startup
	/// announcement all happen from Update, once a few frames have ticked and the
	/// game is live. Thereafter Update pumps focus announcements once per frame.
	/// </summary>
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public partial class Plugin : BaseUnityPlugin {
		public const string PluginGuid = "com.rashad.handoffateaccess";
		public const string PluginName = "Hand of Fate Access";
		// PluginVersion is generated from <Version> in Directory.Build.props at build
		// time (see the GeneratePluginVersion target), so the props file is the single
		// source of truth for the version that both BepInPlugin and the spoken startup
		// line use. BepInPlugin needs a compile-time literal, which the generated const
		// satisfies; the spoken version is read from the assembly at runtime.

		// Frames to wait in Update before initializing, so the game loop is live
		// and the first scene is up before we touch the screen reader.
		private int _initCountdown = StartupDelayFrames;
		private const int StartupDelayFrames = 120;
		private bool _initialized;
		private bool _speechReady;
		private GameScreenWatcher _screenWatcher;
		private ResourceWatcher _resourceWatcher;
		private ProgressWatcher _progressWatcher;
		private InputRouter _input;
		private WallTones _wallTones;
		private CollisionCue _collisionCue;
		private ProjectileSonification _projectiles;
		private ZoneSonification _zones;
		private ObjectBeacons _beacons;
		private AttackCues _attackCues;
		private EnemyLocator _enemyLocator;
		private GambitStatusSpeech _gambitStatus;
		private GambitWatcher _gambit;
		private MapCursor _mapCursor;
		private bool _wasOnMap;
		private GameObject _lastMapSelection;

		private void Awake() {
			LogBepInExBackend.Install(Logger);
			Log.Info("loaded");
		}

		private void Update() {
			if (!_initialized) {
				if (_initCountdown-- > 0) return;
				_initialized = true;
				Initialize();
				return;
			}

			// Wall tones need no screen reader, so they run whether or not speech came up.
			// PlayerMotion feeds both the wall feel and the footstep-suppression patch, so
			// it is refreshed first.
			PlayerMotion.Pump();
			_wallTones.Pump();
			_collisionCue.Pump();
			_projectiles.Pump();
			_zones.Pump();
			_beacons.Pump();
			_attackCues.Pump();
			_gambit.Pump();

			if (_speechReady) {
				_screenWatcher.Pump();
				_resourceWatcher.Pump();
				_progressWatcher.Pump();
				PumpFocus();
				PumpMapScope();
			}

			// Input is pumped with the audio tier, not the speech tier: the enemy locator
			// is a pure audio feature and must answer its key with the screen reader down.
			// It runs after PumpMapScope so the map cursor's gate reads this frame's map
			// state, not last frame's, and a same-frame cursor snap lands before the move.
			_input.Pump();
		}

		// Release the native SAPI voice (and its COM init) on teardown. Other subsystems are
		// left to process exit; the SAPI shim owns an out-of-process COM voice worth releasing.
		private void OnDestroy() {
			_gambitStatus?.Shutdown();
		}

		private void Initialize() {
			Log.Info("update reached; initializing speech");
			Assembly assembly = Assembly.GetExecutingAssembly();
			string pluginDir = Path.GetDirectoryName(assembly.Location);
			NativeLoader.Preload(pluginDir, "Tolk.dll");
			NativeLoader.Preload(pluginDir, "HofSapi.dll");

			// Non-speech audio voice pool. Independent of speech (combat sonification
			// needs no screen reader), so it comes up regardless of the Tolk result. No
			// feature drives it yet; it is the seam the combat and gambit layers register
			// clips and play voices through.
			AudioEngine.Initialize(new UnityAudioBackend());

			// Every spatial feature (wall tones, projectiles, the gambit) conveys position by
			// stereo pan; on a mono output device that cue is gone. Surface it once rather than
			// let those features degrade silently.
			if (AudioSettings.speakerMode == AudioSpeakerMode.Mono)
				Log.Warn("audio output is mono; spatial cues (wall tones, projectiles, gambit) will not localize");

			// Wall tones ride on the audio backend alone, so they come up here, before the
			// speech path, and keep working even if the screen reader never initializes.
			_wallTones = new WallTones();
			_wallTones.Initialize(pluginDir);
			_collisionCue = new CollisionCue();
			_collisionCue.Initialize(pluginDir);
			_projectiles = new ProjectileSonification();
			_projectiles.Initialize();

			// Zone hazards (ground areas, mines, beams, segment chains, traps): polled from
			// the pump off the game's AllAreas registry, the level's scanned traps, and the
			// beams/chains the engage hooks below record, so this needs only the audio backend.
			_zones = new ZoneSonification();
			_zones.Initialize();

			// Chest and exit beacons: walk-in objects found by a periodic scan (they have
			// no registry and no patchable activation), pinged from the pump. Audio only.
			_beacons = new ObjectBeacons();
			_beacons.Initialize(pluginDir);

			// Attack telegraph cues (the block-or-dodge call at each enemy attack). Like the
			// wall tones they ride on the audio backend alone, so they come up here with the
			// other audio features. The hooks that feed them are installed below, gated on audio
			// being live: without it the cues cannot play, so there is no reason to patch the
			// combat path.
			_attackCues = new AttackCues();
			_attackCues.Initialize(pluginDir);
			if (AudioEngine.IsAvailable)
				InstallCombatPatches();

			// The on-demand enemy locator: L on the keyboard and left-stick click on the
			// controller (both verified unbound by the game: L is absent from its default
			// keyboard table and no game action reads LeftStickButton) ping the nearest
			// living enemy. An audio-tier feature, so the input router is created here,
			// before the speech path, and the binding works whether or not the screen
			// reader came up; the speech path registers its own bindings on the same
			// router below. The gate scopes the key to live fights; everywhere else the
			// press is inert.
			_enemyLocator = new EnemyLocator();
			_enemyLocator.Initialize();
			_input = new InputRouter();
			_input.Register(new ButtonAction(
				"enemy locator",
				_enemyLocator.Trigger,
				new[] { KeyCode.L },
				new[] { InControl.InputControlType.LeftStickButton },
				() => CombatEncounter.Instance != null));

			// Log-only diagnostics: the damage tripwire (every player hit names its source) and
			// the proxy reconnaissance (every non-projectile hazard spawn logs its type), both
			// independent of the audio and speech paths so they keep auditing coverage even
			// when the cues themselves failed to come up.
			InstallDiagnosticPatches();

			// The chance gambit's spoken card statuses render through SAPI (for pan and
			// timing) and play off the audio voice pool, so they come up here with the other
			// audio features, independent of the screen-reader speech path. The watcher adds
			// the per-slot identity tones and drives the Establish walk.
			_gambitStatus = new GambitStatusSpeech();
			_gambitStatus.Initialize();
			_gambit = new GambitWatcher(_gambitStatus);
			// Install the gambit hooks here, with the audio path, so they come up whether or not
			// the screen-reader speech path does: the gambit's audio is independent of Tolk. Only
			// patch when the feature is actually available (SAPI rendered the tones/words).
			if (_gambit.Initialize())
				InstallGambitPatches();

			if (!SpeechEngine.Initialize(new TolkBackend())) {
				Log.Warn("speech unavailable; focus announcements disabled");
				return;
			}
			_speechReady = true;

			// Spoken version comes from the assembly (driven by Directory.Build.props),
			// trimmed to major.minor.build so the trailing revision .0 is not read out.
			SpeechPipeline.SpeakInterrupt(Strings.PluginLoaded(assembly.GetName().Version.ToString(3)));
			InstallPatches();

			_screenWatcher = new GameScreenWatcher();
			_screenWatcher.Install();

			_resourceWatcher = new ResourceWatcher();
			_progressWatcher = new ProgressWatcher();

			// Status key: speak the player's resources on demand, anywhere. "/" on the
			// keyboard and right-stick click on the controller, both unbound by the game
			// (verified against its keyboard and controller binding tables), so reading
			// them never competes with a game action and needs no fall-through handling.
			_input.Register(new ButtonAction(
				"status",
				ResourceStatus.Speak,
				new[] { KeyCode.Slash },
				new[] { InControl.InputControlType.RightStickButton }));

			// Map cursor: a free-roam survey of the board, scoped to the map screen. Ctrl+
			// arrows (consumed in the OnKey patch) and the right stick move it; both are free
			// on the map.
			_mapCursor = new MapCursor();
			_input.Register(new DirectionalAction(
				"map cursor",
				_mapCursor.Move,
				() => MapInput.OnMap));

			// A control auto-selected before our patches were live (the main menu's
			// initial button at launch) fired its selection where we couldn't hear
			// it. Seed the current selection so the first focus reads without the
			// user having to move; the dedup in FocusTracker makes this harmless if
			// nothing has been selected yet.
			if (UICamera.selectedObject != null)
				FocusTracker.Record(UICamera.selectedObject);
		}

		private void InstallPatches() {
			var patcher = new HarmonyPatcher(new Harmony(PluginGuid));
			patcher.Patch(
				typeof(UICamera), "SetSelection",
				new[] { typeof(UnityEngine.GameObject), typeof(UICamera.ControlScheme) },
				prefix: null,
				postfix: AccessTools.Method(typeof(UICamera_SetSelection_Patch), "Postfix"));
			patcher.Patch(
				typeof(UISelectable), "Select",
				new[] { typeof(bool) },
				prefix: null,
				postfix: AccessTools.Method(typeof(UISelectable_Select_Patch), "Postfix"));
			patcher.Patch(
				typeof(UISelectable), "OnKey",
				new[] { typeof(UnityEngine.KeyCode) },
				prefix: AccessTools.Method(typeof(UISelectable_OnKey_Patch), "Prefix"),
				postfix: null);
			patcher.Patch(
				typeof(UISelectable), "DoClick",
				new System.Type[0],
				prefix: AccessTools.Method(typeof(UISelectable_DoClick_Patch), "Prefix"),
				postfix: null);
			// A flipped reward card on the end-of-run reward screen is read from this hook:
			// the game moves focus past it before the focus path can, so the reveal is
			// announced from the Update pump instead.
			patcher.Patch(
				typeof(CardSetModifierContainer), "OnCardClicked",
				new[] { typeof(Card) },
				prefix: null,
				postfix: AccessTools.Method(typeof(CardSetModifierContainer_OnCardClicked_Patch), "Postfix"));
			// Suppress the player's footsteps while blocked against a wall. OnFootstep takes
			// the private nested Footstep enum, resolved here to disambiguate it from the
			// two-argument material overload.
			patcher.Patch(
				typeof(Controller), "OnFootstep",
				new[] { AccessTools.Inner(typeof(Controller), "Footstep") },
				prefix: AccessTools.Method(typeof(Controller_OnFootstep_Patch), "Prefix"),
				postfix: null);
		}

		// The combat attack-telegraph hooks, installed with the audio path so they come up whether
		// or not the screen-reader speech path did: the cues are non-speech audio. The cue rides
		// the melee and ranged effect-start calls, fired as each attack's parry window opens,
		// which carry the authoritative blockable flag.
		private void InstallCombatPatches() {
			var patcher = new HarmonyPatcher(new Harmony(PluginGuid + ".combat"));
			patcher.Patch(
				typeof(CombatUtils), "StartMeleeEffect",
				new[] { typeof(EffectSet), typeof(Model), typeof(bool) },
				prefix: AccessTools.Method(typeof(CombatUtils_StartMeleeEffect_Patch), "Prefix"),
				postfix: null);
			patcher.Patch(
				typeof(CombatUtils), "StartRangedEffect",
				new[] { typeof(EffectSet), typeof(Model), typeof(bool) },
				prefix: AccessTools.Method(typeof(CombatUtils_StartRangedEffect_Patch), "Prefix"),
				postfix: null);

			// The bespoke boss attacks: direct-damage actions that bypass the effect-start
			// chokepoint, each cued as a dodge from its own Begin override (every listed class
			// declares one; patching an inherited Begin would hit the shared base and cue every
			// action in the game). The Hermit's bomb hooks its throw event instead, because its
			// Begin runs a multi-second hold first. The mage beams are intentionally absent:
			// they will be voiced live from the proxy seam, not cued as a one-shot.
			var bossBeginPostfix = AccessTools.Method(typeof(BossAction_Begin_Patch), "Postfix");
			var bossActions = new[] {
				typeof(ActionFeastOgre),
				typeof(ActionReaperFinger),
				typeof(ActionReaperScythe),
				typeof(ActionSacrificeLich),
				typeof(ActionSelfOrcShaman),
				typeof(ActionKrakenShock),
				typeof(ActionKrakenTentacleAttack),
				typeof(ActionKrakenTsunami),
				typeof(ActionKrakenSummon),
				typeof(ActionDashRatmanKing),
			};
			foreach (System.Type bossAction in bossActions)
				patcher.Patch(bossAction, "Begin", new System.Type[0], prefix: null, postfix: bossBeginPostfix);
			patcher.Patch(
				typeof(ActionHermitBomb), "OnThrow",
				new System.Type[0],
				prefix: AccessTools.Method(typeof(ActionHermitBomb_OnThrow_Patch), "Prefix"),
				postfix: null);

			// The mover hazards: lobs and lightning heads fly like projectiles but never enter
			// CombatManager's projectile list, so each class's own OnEngage override (both
			// verified to declare one; the base would catch every proxy type) feeds the flight
			// voice and the per-attacker-gated launch cue through one shared postfix.
			var moverEngagePostfix = AccessTools.Method(typeof(CombatProxyMover_OnEngage_Patch), "Postfix");
			patcher.Patch(
				typeof(CombatProxyLob), "OnEngage",
				new System.Type[0],
				prefix: null,
				postfix: moverEngagePostfix);
			patcher.Patch(
				typeof(CombatProxyLightning), "OnEngage",
				new System.Type[0],
				prefix: null,
				postfix: moverEngagePostfix);

			// Beams (damaging lines: the mage triangle, radial bursts, the rotating boss
			// beams) are not CombatProxy subclasses and keep no registry; the one chokepoint
			// every beam passes through is its own Engage, so its postfix is the zone voice's
			// discovery point for them.
			patcher.Patch(
				typeof(CombatProxyBeam), "Engage",
				new[] { typeof(ICombatProxyBeamParent), typeof(Vector3), typeof(float), typeof(float) },
				prefix: null,
				postfix: AccessTools.Method(typeof(CombatProxyBeam_Engage_Patch), "Postfix"));

			// Segment chains (fire trails, lightning paths) keep their live segments in a
			// private list on the parent proxy; each class's own OnEngage override (both
			// verified to declare one) registers the chain for the zone voice. The lightning's
			// flying head is the mover postfix above; this is the damaging path it leaves.
			var chainEngagePostfix = AccessTools.Method(typeof(CombatProxyChain_OnEngage_Patch), "Postfix");
			patcher.Patch(
				typeof(CombatProxyTrail), "OnEngage",
				new System.Type[0],
				prefix: null,
				postfix: chainEngagePostfix);
			patcher.Patch(
				typeof(CombatProxyLightning), "OnEngage",
				new System.Type[0],
				prefix: null,
				postfix: chainEngagePostfix);

			// Traps can come active at any time (a chest or exit can switch a trap hierarchy
			// on mid-level). Trap.Start is an iterator, so its postfix fires at coroutine
			// creation = activation, and the zone pump rescans on the flag.
			patcher.Patch(
				typeof(Trap), "Start",
				new System.Type[0],
				prefix: null,
				postfix: AccessTools.Method(typeof(Trap_Start_Patch), "Postfix"));
		}

		// The log-only diagnostic patches: the damage tripwire on the one chokepoint all damage
		// funnels through, and the proxy reconnaissance pair that records every non-projectile
		// hazard spawn. Their own Harmony group so they install even when the audio path, and
		// with it the combat cue patches, did not: they audit coverage, so they must keep
		// recording when the cues themselves are down.
		private void InstallDiagnosticPatches() {
			var patcher = new HarmonyPatcher(new Harmony(PluginGuid + ".diagnostics"));
			patcher.Patch(
				typeof(Destroyable), "ApplyDamage",
				new[] { typeof(Destroyable.Damage) },
				prefix: AccessTools.Method(typeof(Destroyable_ApplyDamage_Patch), "Prefix"),
				postfix: null);
			patcher.Patch(
				typeof(CombatProxy), "Engage",
				new[] { typeof(CombatEffectProxy), typeof(CombatTarget), typeof(UnityEngine.Transform) },
				prefix: null,
				postfix: AccessTools.Method(typeof(CombatProxy_Engage_Patch), "Postfix"));
			patcher.Patch(
				typeof(CombatProxy), "Disengage",
				new System.Type[0],
				prefix: null,
				postfix: AccessTools.Method(typeof(CombatProxy_Disengage_Patch), "Postfix"));
		}

		// The chance-gambit hooks, kept out of InstallPatches so they install with the audio path
		// regardless of whether the screen-reader speech path came up.
		private void InstallGambitPatches() {
			var patcher = new HarmonyPatcher(new Harmony(PluginGuid + ".gambit"));
			// Chance cards flip face up here just before the held shuffle; the postfix flags it
			// for the gambit pump to run the Establish walk.
			patcher.Patch(
				typeof(CardContainer), "FlipCards",
				new[] { typeof(bool), typeof(bool) },
				prefix: null,
				postfix: AccessTools.Method(typeof(CardContainer_FlipCards_Patch), "Postfix"));
			// The shuffle coroutine starting is the cue to follow the cards by ear. AnimatedShuffle
			// is an iterator, so the postfix fires when the coroutine is created, as it begins.
			patcher.Patch(
				typeof(CardChoiceContainer), "AnimatedShuffle",
				new[] { typeof(int), typeof(float) },
				prefix: null,
				postfix: AccessTools.Method(typeof(CardChoiceContainer_AnimatedShuffle_Patch), "Postfix"));
		}

		// The focused control and its last spoken readout, kept so an in-place value
		// change (a selector/toggle that updates its label without moving focus) is
		// detected by re-reading it each frame. _watchedHasValue records whether the
		// control separates a value readout, which decides its poll mode.
		private GameObject _watched;
		private string _watchedReadout;
		private bool _watchedHasValue;

		private void PumpFocus() {
			// The rebind scan edge poll runs before anything else so a scan edge is
			// never deferred behind a focus announcement frame.
			PumpBindingScan();

			if (FocusTracker.TryConsume(out var go, out var userInitiated)) {
				string announcement = BuildReadout(go, out string valueReadout);
				// Watch only a control that actually said something, so a suppressed or
				// empty focus is not re-polled (which would re-log every frame).
				_watched = string.IsNullOrEmpty(announcement) ? null : go;
				_watchedReadout = announcement;
				_watchedHasValue = !string.IsNullOrEmpty(valueReadout);
				if (string.IsNullOrEmpty(announcement)) return;

				// A screen or overlay that announced itself this frame leads: the focus
				// queues behind it so it reads after the screen name or dialogue text. With
				// no fresh context, a focus the user drove with a key interrupts (responsive
				// navigation) and one the game landed on its own queues.
				SpeechMode mode = FocusAnnouncePolicy.Decide(userInitiated, _screenWatcher.ConsumeScreenJustChanged());
				if (mode == SpeechMode.Interrupt)
					SpeechPipeline.SpeakInterrupt(announcement);
				else
					SpeechPipeline.SpeakQueued(announcement);
				return;
			}

			PollWatchedValue();
		}

		// Selectors and toggles change their value in place (e.g. the language picker
		// rewrites its label on left/right) without firing a selection change, so they
		// are invisible to the focus path. While the watched control is still the live
		// selection, re-read it and speak when its readout changed. ValuePollPolicy
		// decides whether to poll this frame and whether the change interrupts or queues.
		private void PollWatchedValue() {
			if (_watched == null) return;
			if (UICamera.selectedObject != _watched) return;

			// A control that separates a value is polled every frame like a stat card:
			// its applied value can land frames after the input that caused it (a
			// fullscreen or resolution switch completes when the engine gets to it; a
			// rebound key's label updates in a game coroutine after the scan).
			bool alwaysPoll = _watchedHasValue || _watched.GetComponentInParent<StatCard>() != null;
			bool wasRecentInput = NavigationState.WasRecent;
			if (!ValuePollPolicy.ShouldPoll(alwaysPoll, wasRecentInput)) return;

			string current = BuildReadout(_watched, out string valueOnly);
			// A failed or transiently empty re-read keeps the last good readout:
			// advancing the stored readout would make the recovery frame look like a
			// change and speak a stale value. The failure itself is already logged.
			if (string.IsNullOrEmpty(current)) return;
			if (current == _watchedReadout) return;
			_watchedReadout = current;

			// A control that separates its value (a settings row) re-speaks only the
			// value: the title did not change and was spoken when focus landed, and a
			// held adjustment reads much faster as "80%, 90%" than as the full row.
			string speech = string.IsNullOrEmpty(valueOnly) ? current : valueOnly;

			if (ValuePollPolicy.Delivery(alwaysPoll, wasRecentInput) == SpeechMode.Interrupt)
				SpeechPipeline.SpeakInterrupt(speech);
			else
				SpeechPipeline.SpeakQueued(speech);
		}

		// Rebind feedback on the controls screen, invisible to both the focus path and
		// the label poll: clicking a binding row starts cInput's key scan (the game
		// shows only a colour change), so the listening prompt is spoken on that edge.
		// The key that ends the scan goes to cInput before UICamera can see it, so the
		// end edge stamps NavigationState (it is user input); when the scan changed
		// nothing (the same key re-pressed, or cancelled; this cInput build has no
		// timeout), the unchanged value is re-spoken a few frames later so the player
		// hears the scan is over instead of a dangling prompt.
		private bool _wasScanning;
		private int _scanConfirmFrame = -1;
		private string _scanEndReadout;
		// Frames between the scan-end edge and the unchanged-binding confirmation:
		// enough for the row's label to update (a game coroutine that runs after this
		// pump) and the value poll to speak a real change first.
		private const int ScanConfirmDelayFrames = 3;

		private void PumpBindingScan() {
			bool scanning = cInput.scanning;
			if (scanning != _wasScanning) {
				_wasScanning = scanning;
				// Read the binding row off the live selection, not the watch state, so
				// an unrelated readout failure cannot also mute the prompt.
				GameObject selected = UICamera.selectedObject;
				bool bindingFocused = selected != null && selected.GetComponentInParent<ControlBindElement>() != null;
				if (scanning) {
					if (bindingFocused)
						SpeechPipeline.SpeakInterrupt(Strings.BindingPressKey);
				} else if (bindingFocused) {
					NavigationState.Mark();
					_scanConfirmFrame = Time.frameCount + ScanConfirmDelayFrames;
					_scanEndReadout = _watchedReadout;
				}
			}

			if (_scanConfirmFrame >= 0 && Time.frameCount >= _scanConfirmFrame) {
				_scanConfirmFrame = -1;
				// The value poll spoke already if the readout moved by now; an unchanged
				// readout means the scan ended with the binding as it was.
				if (_watched != null && UICamera.selectedObject == _watched && _watchedReadout == _scanEndReadout) {
					string current = BuildReadout(_watched, out string valueOnly);
					string speech = string.IsNullOrEmpty(valueOnly) ? current : valueOnly;
					if (!string.IsNullOrEmpty(speech))
						SpeechPipeline.SpeakInterrupt(speech);
				}
			}
		}

		// Publish whether the map is the active screen, so the cursor's input binding and the
		// OnKey patch agree on when Ctrl+arrows belong to the cursor. Re-anchor the cursor on
		// the player each time the map becomes active (after launch or returning from an
		// encounter), so it always opens where the player stands. While on the map, snap the
		// free cursor onto the game's own selected slot whenever that selection moves (plain
		// arrows), so the free cursor follows the game's cursor. Our own Ctrl+arrow moves are
		// swallowed in the OnKey patch, so they never change the game's selection and so never
		// trigger this reset.
		private void PumpMapScope() {
			bool onMap = _screenWatcher.Stack.Top == ScreenId.Map;
			MapInput.OnMap = onMap;
			if (onMap) {
				if (!_wasOnMap)
					_mapCursor.OnMapEntered();
				else if (UICamera.selectedObject != _lastMapSelection)
					_mapCursor.AnchorToGameCursor();
				_lastMapSelection = UICamera.selectedObject;
			}
			_wasOnMap = onMap;
		}

		// Reading the focused control touches live game model state (e.g.
		// EncounterCard.Description), which can throw on malformed asset data. Catch at
		// this boundary so a bad control logs instead of silently dropping the readout.
		// Returns null for a suppressed (structural) or unreadable control.
		private string BuildReadout(GameObject go) {
			string ignored;
			return BuildReadout(go, out ignored);
		}

		// valueReadout is the value part alone for a control that separates one (a
		// settings row), used by the poll to speak just the changed value; null for
		// everything else.
		private string BuildReadout(GameObject go, out string valueReadout) {
			valueReadout = null;
			// Read the name inside the try: a destroyed Unity object throws on .name too,
			// and that must not escape the catch unlogged.
			string label = "focus";
			try {
				label = go.name;
				// While picking a shuffled chance card, speak its current slot number instead of
				// the identical "face down card", so the player can reach the slot they tracked.
				if (_gambit != null && _gambit.TrySlotName(go, out int slot))
					return Strings.GambitSlot(slot);
				UIElement element = ProxyFactory.Create(go);
				if (element == null) {
					// A structural selectable (group/blocker) grabbed focus with no content
					// of its own. Not an error: the delegated child speaks. Logged so a
					// genuinely stranded focus is traceable instead of silently dropped.
					Log.Debug("focus suppressed for structural selectable '" + label + "'");
					return null;
				}
				Message value = element.DescribeValue();
				if (value != null)
					valueReadout = value.Resolve();
				return element.Describe().Resolve();
			} catch (Exception ex) {
				Log.Error("focus readout failed for '" + label + "': " + ex);
				return null;
			}
		}
	}
}
