using System;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The wall-tone combat aid: while a fight is live, a looping wind for each side
	/// (left, right, ahead, behind) swells as an impassable wall or column closes in, so
	/// a player who cannot see the arena can hear how boxed in they are and which way is
	/// clear. The four clips are authored mono wind loops loaded from the plugin's sounds
	/// folder, distinct timbres so simultaneous close walls stay readable (see the Core
	/// composer's remarks). Each side wind is hard-panned into its ear and stays there;
	/// volume alone carries distance.
	///
	/// All four voices run continuously for the whole fight; each frame their volumes are
	/// eased toward the targets the Core composer derives from the live distances. Driving
	/// the running loops rather than starting and stopping them is
	/// deliberate: in cluttered arenas the nearest object on a side flickers across the
	/// range edge and swaps identity constantly, and restarting a loop each time clicks
	/// and lurches. The voice handles are the mod's own audio state, not game state; the
	/// distances behind them are re-measured live every frame.
	/// </summary>
	internal sealed class WallTones {
		// Indexed by (int)WallSide: Right, Left, Above, Below. The keys (and the
		// sounds-folder file stems they double as) live on the Core composer, which the
		// sound glossary also reads.
		private static readonly string[] Keys = {
			WallToneComposer.RightKey,
			WallToneComposer.LeftKey,
			WallToneComposer.AboveKey,
			WallToneComposer.BelowKey,
		};

		// Fully qualified: the game's Assembly-CSharp has its own global Voice type that
		// otherwise shadows the audio pool's handle here.
		private readonly HandOfFateAccess.Audio.Voice[] _voice = new HandOfFateAccess.Audio.Voice[4];
		private readonly float[] _volume = new float[4];
		private readonly bool[] _loaded = new bool[4];
		private bool _started;

		/// <summary>
		/// Load and register the four tone clips from <paramref name="pluginDir"/>/sounds.
		/// A clip that fails to load leaves its side silent (logged), so a missing or bad
		/// file degrades one direction rather than the whole feature.
		/// </summary>
		public void Initialize(string pluginDir) {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; wall tones disabled");
				return;
			}
			string soundsDir = Path.Combine(pluginDir, "sounds");
			for (int i = 0; i < Keys.Length; i++)
				_loaded[i] = LoadClip(soundsDir, Keys[i]);
		}

		private bool LoadClip(string soundsDir, string key) {
			string path = Path.Combine(soundsDir, key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(key, pcm, channels, sampleRate);
				return true;
			} catch (Exception ex) {
				Log.Error("wall tone '" + key + "' failed to load from " + path + ": " + ex);
				return false;
			}
		}

		/// <summary>
		/// Drive the tones for this frame. Outside a live fight (or before audio is up) the
		/// voices are stopped; inside combat each side's volume and pan ease toward their
		/// live targets.
		/// </summary>
		public void Pump() {
			if (!AudioEngine.IsAvailable) return;

			// The Dealer's missile quick-time counts as outside: the player is teleported
			// onto an authored platform there, so the probe would measure geometry that
			// says nothing about where the player can walk.
			if (!CombatGate.IsLive || DealerQte.IsActive) {
				StopSession();
				return;
			}

			WallProbe probe = CombatArenaReader.Probe(WallToneComposer.Range);
			if (probe == null) {
				StopSession();
				return;
			}

			if (!_started) StartSession();

			float dt = Time.deltaTime;
			for (int i = 0; i < 4; i++)
				Drive((WallSide)i, probe, dt);
		}

		// Open every loaded side's voice at silence and at its fixed hard pan; the per-frame
		// easing brings the volume up from there, so the fight starts without a click.
		private void StartSession() {
			_started = true;
			for (int i = 0; i < 4; i++) {
				_volume[i] = 0f;
				if (!_loaded[i]) continue;
				_voice[i] = AudioEngine.Play(Keys[i], new SoundParams(WallToneComposer.PanFor((WallSide)i), 1f, 0f), true);
			}
			Log.Debug("wall tones started");
		}

		private void Drive(WallSide side, WallProbe probe, float dt) {
			int i = (int)side;
			if (!_voice[i].IsValid) return;
			float distance = probe.DistanceTo(side);
			_volume[i] = WallToneComposer.Smooth(_volume[i], WallToneComposer.TargetVolume(distance), dt);
			AudioEngine.Update(_voice[i], new SoundParams(WallToneComposer.PanFor(side), 1f, _volume[i]));
		}

		private void StopSession() {
			if (!_started) return;
			_started = false;
			for (int i = 0; i < 4; i++) {
				if (_voice[i].IsValid) AudioEngine.Stop(_voice[i]);
				_voice[i] = HandOfFateAccess.Audio.Voice.None;
				_volume[i] = 0f;
			}
			Log.Debug("wall tones stopped");
		}
	}
}
