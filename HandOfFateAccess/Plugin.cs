using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Focus;
using HandOfFateAccess.Patches;
using HandOfFateAccess.Patching;
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

			if (_speechReady)
				PumpFocus();
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
		}

		private void InstallPatches() {
			var patcher = new HarmonyPatcher(new Harmony(PluginGuid));
			patcher.Patch(
				typeof(UICamera), "SetSelection",
				new[] { typeof(UnityEngine.GameObject), typeof(UICamera.ControlScheme) },
				prefix: null,
				postfix: AccessTools.Method(typeof(UICamera_SetSelection_Patch), "Postfix"));
		}

		private void PumpFocus() {
			if (!FocusTracker.TryConsume(out var go)) return;

			FocusDto dto = FocusAdapter.Extract(go);
			string announcement = FocusComposer.Compose(dto);
			if (!string.IsNullOrEmpty(announcement))
				SpeechPipeline.SpeakInterrupt(announcement);
		}
	}
}
