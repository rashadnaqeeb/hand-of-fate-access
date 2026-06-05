using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Patches;
using HandOfFateAccess.Patching;
using HandOfFateAccess.Screens;
using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;
using HarmonyLib;

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
			string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			NativeLoader.Preload(pluginDir, "Tolk.dll");

			if (!SpeechEngine.Initialize(new TolkBackend())) {
				Log.Warn("speech unavailable; focus announcements disabled");
				return;
			}
			_speechReady = true;

			SpeechPipeline.SpeakInterrupt("Hand of Fate Access loaded");
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
		}

		private void PumpFocus() {
			if (!FocusTracker.TryConsume(out var go)) return;

			// Reading the focused control touches live game model state (e.g.
			// EncounterCard.Description), which can throw on malformed asset data.
			// Catch at this boundary so a bad card logs once instead of silently
			// dropping the focus announcement. Runs per focus change, not per frame.
			string announcement;
			// Read the name inside the try: a destroyed Unity object throws on .name
			// too, and that must not escape the catch unlogged. If it throws here the
			// label stays generic and the failure is still reported.
			string label = "focus";
			try {
				label = go.name;
				announcement = ProxyFactory.Create(go).Describe().Resolve();
			} catch (Exception ex) {
				Log.Error("focus readout failed for '" + label + "': " + ex);
				return;
			}

			if (string.IsNullOrEmpty(announcement)) return;

			// Entering a screen auto-selects a control, firing this focus change in
			// the same beat. When that happens, speak the control queued so it reads
			// after the screen name instead of cutting it off; otherwise interrupt.
			if (_screenWatcher.ConsumeScreenJustChanged())
				SpeechPipeline.SpeakQueued(announcement);
			else
				SpeechPipeline.SpeakInterrupt(announcement);
		}
	}
}
