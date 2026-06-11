using System;
using System.Collections.Generic;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The walk-in beacons: the objects a level ends or rewards through when the player
	/// stands on them. Two come from the trap system (the game's <c>TrapChest</c> and
	/// <c>TrapExit</c>; the latter is the completion trigger <c>CombatEncounter</c> wires
	/// for trap rooms and authored escapes). The third is <c>AICourtTrigger</c>, the court
	/// (face card) fights' own end circle: the spawner drops it at the boss's corpse, and
	/// the fight does not end on the kill - the player must walk into it. Each live,
	/// unconsumed object pings with its kind's sample (the court circle borrows the exit's:
	/// to the player it IS the exit), positioned by Core's <see cref="BeaconComposer"/>,
	/// repinging a beat of silence after the sound ends.
	///
	/// Discovery for the trap pair is a periodic <c>FindObjectsOfType</c> scan: the
	/// components have no registry, no Start or OnEnable to patch, and they switch on
	/// mid-level (a chest's enable-on-complete list can activate the exit, or more of the
	/// gauntlet), so only a rescan sees them appear. The scan returns active objects only,
	/// which is the right filter: an exit the level has not yet revealed does not exist for
	/// the player either. The court trigger needs no scan (a public static singleton) and
	/// pings only while its <c>IsActive</c> is true - the spawner arms it (and its collider)
	/// only once it is the last active spawner with no living AI, so an earlier ping would
	/// lure the player onto a circle that does nothing yet. All found components are live
	/// references re-read every frame (the acceptable cache); IsComplete, IsActive,
	/// activity, and position are never copied.
	/// </summary>
	internal sealed class ObjectBeacons {
		private const float ScanInterval = 1f;

		private bool _ready;
		// Each loaded sample's authored duration in seconds, keyed by sample key. Doubles as
		// the loaded set: a missing file degrades that one beacon (logged at load) instead of
		// asking the backend for an unregistered key every ping. The duration schedules the
		// next ping a fixed gap after this one's sound ends.
		private readonly Dictionary<string, float> _clips = new Dictionary<string, float>();

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
		private bool _courtWasActive;

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
			Log.Debug("object beacons ready");
		}

		private void LoadSample(string soundsDir, string key) {
			string path = Path.Combine(soundsDir, key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(key, pcm, channels, sampleRate);
				_clips[key] = pcm.Length / (float)channels / sampleRate;
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
			// The Dealer's missile quick-time counts as outside: the player is teleported and
			// the camera overridden, so a bearing projected there would mislead.
			if (CombatManager.Instance == null || DealerQte.IsActive
					|| !CombatFrame.TryGet(out CombatFrame frame)) {
				Reset();
				return;
			}

			float now = Time.time;
			if (!_inLevel) {
				_inLevel = true;
				_nextScan = now;
				_nextChestPing = now;
				_nextExitPing = now + BeaconComposer.ExitStagger;
			}

			if (now >= _nextScan) {
				Scan();
				_nextScan = now + ScanInterval;
			}

			// The court circle's arming edge, logged like the scan counts: recon for a boss
			// fight that ends with no beacon heard.
			AICourtTrigger court = AICourtTrigger.Instance;
			bool courtActive = court != null && court.IsActive;
			if (courtActive != _courtWasActive) {
				Log.Info("beacons: court trigger " + (courtActive ? "active" : "inactive"));
				_courtWasActive = courtActive;
			}

			// Each kind repings once its sound has ended plus the gap; a frame where nothing
			// played (no object live, or the player standing on it) re-checks after the bare
			// gap. With several objects of one kind the slowest ping sets the cadence, so the
			// gap is silence after ALL of them.
			if (now >= _nextChestPing) {
				_nextChestPing = now + BeaconComposer.PingGap;
				if (_clips.TryGetValue(BeaconComposer.ChestKey, out float chestClip))
					for (int i = 0; i < _chests.Length; i++) {
						TrapChest chest = _chests[i];
						if (chest == null || chest.IsComplete || !chest.gameObject.activeInHierarchy) continue;
						if (Ping(BeaconComposer.ChestKey, chest.transform.position, frame, out float pitch)) {
							float next = BeaconComposer.NextPingTime(now, chestClip, pitch);
							if (next > _nextChestPing) _nextChestPing = next;
						}
					}
			}

			if (now >= _nextExitPing) {
				_nextExitPing = now + BeaconComposer.PingGap;
				if (_clips.TryGetValue(BeaconComposer.ExitKey, out float exitClip)) {
					for (int i = 0; i < _exits.Length; i++) {
						TrapExit exit = _exits[i];
						if (exit == null || exit.IsComplete || !exit.gameObject.activeInHierarchy) continue;
						if (Ping(BeaconComposer.ExitKey, exit.transform.position, frame, out float pitch)) {
							float next = BeaconComposer.NextPingTime(now, exitClip, pitch);
							if (next > _nextExitPing) _nextExitPing = next;
						}
					}
					// The armed court circle rides the exit cadence: standing in it is how a
					// boss fight ends, so to the player it is the exit. Walking in completes
					// the encounter and the out-of-level reset silences it; a reanimating boss
					// destroys the trigger, which the null check sees next ping.
					if (courtActive && Ping(BeaconComposer.ExitKey, court.transform.position, frame, out float courtPitch)) {
						float next = BeaconComposer.NextPingTime(now, exitClip, courtPitch);
						if (next > _nextExitPing) _nextExitPing = next;
					}
				}
			}
		}

		// Plays the ping and reports the pitch it played at (a playback-rate multiplier, so
		// the caller can tell when this sound will end); false when the ping was suppressed.
		private static bool Ping(string key, Vector3 position, CombatFrame frame, out float pitch) {
			frame.Project(position, out float right, out float forward);
			if (!BeaconComposer.TryCompose(right, forward, out SoundParams parameters)) {
				pitch = 1f;
				return false;
			}
			AudioEngine.PlayOneShot(key, parameters);
			pitch = parameters.Pitch;
			return true;
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
			_courtWasActive = false;
		}
	}
}
