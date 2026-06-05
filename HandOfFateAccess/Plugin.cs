using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Localization;
using HandOfFateAccess.Patches;
using HandOfFateAccess.Patching;
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
	public class Plugin : BaseUnityPlugin {
		public const string PluginGuid = "com.rashad.handoffateaccess";
		public const string PluginName = "Hand of Fate Access";
		// BepInPlugin needs a compile-time literal, so this cannot read the assembly.
		// Keep it in sync with <Version> in Directory.Build.props (the source the
		// spoken startup version is read from); bumping one means bumping both.
		public const string PluginVersion = "0.1.0";

		// Frames to wait in Update before initializing, so the game loop is live
		// and the first scene is up before we touch the screen reader.
		private int _initCountdown = StartupDelayFrames;
		private const int StartupDelayFrames = 120;
		private bool _initialized;
		private bool _speechReady;
		private GameScreenWatcher _screenWatcher;

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
				PumpFocus();
			}
		}

		private void Initialize() {
			Log.Info("update reached; initializing speech");
			Assembly assembly = Assembly.GetExecutingAssembly();
			string pluginDir = Path.GetDirectoryName(assembly.Location);
			NativeLoader.Preload(pluginDir, "Tolk.dll");

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
		// selection, re-read it and speak when its readout changed. Interrupt, since the
		// change is the user's direct response to their own input. Gated to just after
		// input so it does not rebuild the readout every idle frame, and so a label that
		// changes on its own (not from the user) is not announced as if they changed it.
		private void PollWatchedValue() {
			if (_watched == null || !NavigationState.WasRecent) return;
			if (UICamera.selectedObject != _watched) return;
			string current = BuildReadout(_watched);
			if (current == _watchedReadout) return;
			_watchedReadout = current;
			if (!string.IsNullOrEmpty(current))
				SpeechPipeline.SpeakInterrupt(current);
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
