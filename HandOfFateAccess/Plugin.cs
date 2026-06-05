using System.IO;
using System.Reflection;
using BepInEx;
using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;

namespace HandOfFateAccess {
	/// <summary>
	/// BepInEx entry point. Awake does only non-Unity setup (logging); everything
	/// else is deferred to the Update loop. Awake runs inside the BepInEx
	/// chainloader during Application's static constructor, before the engine
	/// main loop is running: touching Unity APIs there crashes, and initializing
	/// the screen-reader bridge there appears to interfere with the game's
	/// startup. Native preload, Tolk init, and the startup announcement all
	/// happen from Update, once a few frames have ticked and the game is live.
	/// </summary>
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class Plugin : BaseUnityPlugin {
		public const string PluginGuid = "com.rashad.handoffateaccess";
		public const string PluginName = "Hand of Fate Access";
		public const string PluginVersion = "0.1.0";

		// Frames to wait in Update before initializing speech, so the game loop
		// is live and the first scene is up before we touch the screen reader.
		private int _initCountdown = StartupDelayFrames;
		private const int StartupDelayFrames = 120;
		private bool _done;

		private void Awake() {
			LogBepInExBackend.Install(Logger);
			Log.Info("loaded");
		}

		private void Update() {
			if (_done) return;
			if (_initCountdown-- > 0) return;
			_done = true;

			Log.Info("update reached; initializing speech");
			string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			NativeLoader.Preload(pluginDir, "Tolk.dll");

			if (!SpeechEngine.Initialize(new TolkBackend())) {
				Log.Warn("speech unavailable; no startup line spoken");
				return;
			}

			Log.Info("speaking startup line");
			SpeechPipeline.SpeakInterrupt("Hand of Fate Access loaded");
			Log.Info("startup line dispatched");
		}
	}
}
