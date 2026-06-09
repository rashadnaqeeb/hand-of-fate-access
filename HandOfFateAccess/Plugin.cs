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
		private AttackCues _attackCues;
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
			_attackCues.Pump();
			_gambit.Pump();

			if (_speechReady) {
				_screenWatcher.Pump();
				_resourceWatcher.Pump();
				_progressWatcher.Pump();
				PumpFocus();
				PumpMapScope();
				_input.Pump();
			}
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

			// Zone hazards (ground areas, mines): no hooks, the game's own AllAreas registry
			// is polled from the pump, so this needs only the audio backend.
			_zones = new ZoneSonification();
			_zones.Initialize();

			// Attack telegraph cues (the block-or-dodge call at each enemy attack). Like the
			// wall tones they ride on the audio backend alone, so they come up here with the
			// other audio features. The hooks that feed them are installed below, gated on audio
			// being live: without it the cues cannot play, so there is no reason to patch the
			// combat path.
			_attackCues = new AttackCues();
			_attackCues.Initialize(pluginDir);
			if (AudioEngine.IsAvailable)
				InstallCombatPatches();

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
			_input = new InputRouter();
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
		// detected by re-reading it each frame.
		private GameObject _watched;
		private string _watchedReadout;

		private void PumpFocus() {
			if (FocusTracker.TryConsume(out var go, out var userInitiated)) {
				string announcement = BuildReadout(go);
				// Watch only a control that actually said something, so a suppressed or
				// empty focus is not re-polled (which would re-log every frame).
				_watched = string.IsNullOrEmpty(announcement) ? null : go;
				_watchedReadout = announcement;
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

			bool isStat = _watched.GetComponentInParent<StatCard>() != null;
			bool wasRecentInput = NavigationState.WasRecent;
			if (!ValuePollPolicy.ShouldPoll(isStat, wasRecentInput)) return;

			string current = BuildReadout(_watched);
			if (current == _watchedReadout) return;
			_watchedReadout = current;
			if (string.IsNullOrEmpty(current)) return;

			if (ValuePollPolicy.Delivery(isStat, wasRecentInput) == SpeechMode.Interrupt)
				SpeechPipeline.SpeakInterrupt(current);
			else
				SpeechPipeline.SpeakQueued(current);
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
				return element.Describe().Resolve();
			} catch (Exception ex) {
				Log.Error("focus readout failed for '" + label + "': " + ex);
				return null;
			}
		}
	}
}
