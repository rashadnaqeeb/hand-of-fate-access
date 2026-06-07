using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Focus;
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

			if (_speechReady) {
				_screenWatcher.Pump();
				_resourceWatcher.Pump();
				_progressWatcher.Pump();
				PumpFocus();
				PumpMapScope();
				_input.Pump();
			}
		}

		private void Initialize() {
			Log.Info("update reached; initializing speech");
			Assembly assembly = Assembly.GetExecutingAssembly();
			string pluginDir = Path.GetDirectoryName(assembly.Location);
			NativeLoader.Preload(pluginDir, "Tolk.dll");

			// Non-speech audio voice pool. Independent of speech (combat sonification
			// needs no screen reader), so it comes up regardless of the Tolk result. No
			// feature drives it yet; it is the seam the combat and gambit layers register
			// clips and play voices through.
			AudioEngine.Initialize(new UnityAudioBackend());

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
