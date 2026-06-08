using System;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The wall-tone combat aid: while a fight is live, a looping tone for each side
	/// (left, right, ahead, behind) swells as an impassable wall or column closes in, so
	/// a player who cannot see the arena can hear how boxed in they are and which way is
	/// clear. The four clips are authored mono wind loops loaded from the plugin's sounds
	/// folder; the engine pans them at play time, so the spatial position is exact.
	///
	/// All four voices run continuously for the whole fight; each frame their volumes are
	/// eased toward the targets the Core composer derives from the live distances. Driving
	/// volume rather than starting and stopping loops is deliberate: in cluttered arenas
	/// the nearest object on a side flickers across the range edge and swaps identity
	/// constantly, and restarting a loop each time clicks and lurches. The voice handles
	/// are the mod's own audio state, not game state; the distances behind them are
	/// re-measured live every frame.
	/// </summary>
	internal sealed class WallTones {
		// Indexed by (int)WallSide: Right, Left, Above, Below. The keys double as the
		// sounds-folder file stems (walltone_right.wav ...).
		private static readonly string[] Keys = {
			"walltone_right",
			"walltone_left",
			"walltone_above",
			"walltone_below",
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
		/// Drive the tones for this frame. Outside combat (or before audio is up) the
		/// voices are stopped; inside combat each side's volume eases toward its live target.
		/// </summary>
		public void Pump() {
			if (!AudioEngine.IsAvailable) return;

			if (CombatEncounter.Instance == null) {
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

		// Open every loaded side's voice at silence; the per-frame easing brings each up
		// from there, so the fight starts without a click.
		private void StartSession() {
			_started = true;
			for (int i = 0; i < 4; i++) {
				_volume[i] = 0f;
				if (!_loaded[i]) continue;
				var side = (WallSide)i;
				_voice[i] = AudioEngine.Play(Keys[i], new SoundParams(WallToneComposer.PanFor(side), 1f, 0f), true);
			}
			Log.Debug("wall tones started");
		}

		private void Drive(WallSide side, WallProbe probe, float dt) {
			int i = (int)side;
			if (!_voice[i].IsValid) return;
			float target = WallToneComposer.TargetVolume(probe.DistanceTo(side));
			_volume[i] = WallToneComposer.Smooth(_volume[i], target, dt);
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
