using System;
using System.Collections.Generic;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The chest and exit beacons: the two walk-in objects a level can hold (the game's
	/// <c>TrapChest</c> and <c>TrapExit</c>, which despite the names are its only such
	/// components - every exit, trap room or boss escape, is a <c>TrapExit</c>, the one
	/// object <c>CombatEncounter</c> wires as the level's completion trigger). Each live,
	/// unconsumed object pings on its cadence with its own sample, positioned by Core's
	/// <see cref="BeaconComposer"/>, until the player walks in and its <c>IsComplete</c>
	/// flips.
	///
	/// Discovery is a periodic <c>FindObjectsOfType</c> scan: the components have no
	/// registry, no Start or OnEnable to patch, and they switch on mid-level (a chest's
	/// enable-on-complete list can activate the exit, or more of the gauntlet), so only a
	/// rescan sees them appear. The scan returns active objects only, which is the right
	/// filter: an exit the level has not yet revealed does not exist for the player either.
	/// The found components are live references re-read every frame (the acceptable cache);
	/// IsComplete, activity, and position are never copied.
	/// </summary>
	internal sealed class ObjectBeacons {
		private const float ScanInterval = 1f;

		private bool _ready;
		// Sample keys that actually loaded, so a missing file degrades that one beacon
		// (logged at load) instead of asking the backend for an unregistered key every ping.
		private readonly HashSet<string> _loaded = new HashSet<string>();

		private TrapChest[] _chests = new TrapChest[0];
		private TrapExit[] _exits = new TrapExit[0];
		private bool _inLevel;
		private float _nextScan;
		private float _nextChestPing;
		private float _nextExitPing;
		// Last logged counts so the discovery line fires only on change. Doubles as recon:
		// a level with a visible exit but a zero count here means an exit type the scan
		// does not cover.
		private int _lastChests = -1;
		private int _lastExits = -1;

		/// <summary>Load the two beacon samples from <paramref name="pluginDir"/>/sounds.
		/// A sample that fails to load leaves that beacon silent (logged), so a missing
		/// file degrades one beacon rather than the feature.</summary>
		public void Initialize(string pluginDir) {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; object beacons disabled");
				return;
			}
			string soundsDir = Path.Combine(pluginDir, "sounds");
			LoadSample(soundsDir, BeaconComposer.ChestKey);
			LoadSample(soundsDir, BeaconComposer.ExitKey);
			_ready = true;
		}

		private void LoadSample(string soundsDir, string key) {
			string path = Path.Combine(soundsDir, key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(key, pcm, channels, sampleRate);
				_loaded.Add(key);
			} catch (Exception ex) {
				Log.Error("beacon sample '" + key + "' failed to load from " + path + ": " + ex);
			}
		}

		/// <summary>
		/// Drive the beacons for this frame: rescan on the timer, ping each kind on its
		/// cadence. Outside level play (no combat manager, or no player/camera) the state
		/// resets so the next level starts with a fresh scan and cadence.
		/// </summary>
		public void Pump() {
			if (!_ready) return;
			if (CombatManager.Instance == null || !CombatFrame.TryGet(out CombatFrame frame)) {
				Reset();
				return;
			}

			float now = Time.time;
			if (!_inLevel) {
				_inLevel = true;
				_nextScan = now;
				_nextChestPing = now;
				_nextExitPing = now + BeaconComposer.ExitPhaseOffset;
			}

			if (now >= _nextScan) {
				Scan();
				_nextScan = now + ScanInterval;
			}

			if (now >= _nextChestPing) {
				_nextChestPing = now + BeaconComposer.PingInterval;
				if (_loaded.Contains(BeaconComposer.ChestKey))
					for (int i = 0; i < _chests.Length; i++) {
						TrapChest chest = _chests[i];
						if (chest == null || chest.IsComplete || !chest.gameObject.activeInHierarchy) continue;
						Ping(BeaconComposer.ChestKey, chest.transform.position, frame);
					}
			}

			if (now >= _nextExitPing) {
				_nextExitPing = now + BeaconComposer.PingInterval;
				if (_loaded.Contains(BeaconComposer.ExitKey))
					for (int i = 0; i < _exits.Length; i++) {
						TrapExit exit = _exits[i];
						if (exit == null || exit.IsComplete || !exit.gameObject.activeInHierarchy) continue;
						Ping(BeaconComposer.ExitKey, exit.transform.position, frame);
					}
			}
		}

		private static void Ping(string key, Vector3 position, CombatFrame frame) {
			frame.Project(position, out float right, out float forward);
			if (BeaconComposer.TryCompose(right, forward, out SoundParams parameters))
				AudioEngine.PlayOneShot(key, parameters);
		}

		private void Scan() {
			_chests = UnityEngine.Object.FindObjectsOfType<TrapChest>();
			_exits = UnityEngine.Object.FindObjectsOfType<TrapExit>();
			if (_chests.Length != _lastChests || _exits.Length != _lastExits) {
				Log.Info("beacons: " + _chests.Length + " chest(s), " + _exits.Length + " exit(s)");
				_lastChests = _chests.Length;
				_lastExits = _exits.Length;
			}
		}

		private void Reset() {
			if (!_inLevel) return;
			_inLevel = false;
			_chests = new TrapChest[0];
			_exits = new TrapExit[0];
			_lastChests = -1;
			_lastExits = -1;
		}
	}
}
