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
	/// for trap rooms and authored escapes). <c>Loot</c> is the loose pickups (gold piles
	/// pre-placed in trap rooms, gold and food dropped in fights), which collect by the same
	/// walk-in rule and so borrow the chest's voice: to the player both mean "treasure,
	/// stand here". The last is <c>AICourtTrigger</c>, the court (face card) fights' own end
	/// circle: the spawner drops it at the boss's corpse, and the fight does not end on the
	/// kill - the player must walk into it. Each live, unconsumed object pings with its
	/// kind's sample (the court circle borrows the exit's: to the player it IS the exit),
	/// positioned by Core's <see cref="BeaconComposer"/>, repinging a beat of silence after
	/// the sound ends.
	///
	/// Except treasure in a trap room (a level the game wires a <c>TrapExit</c> for, read
	/// off the public <c>CombatEncounter.HasTrapExit</c>): those rooms seed a dozen gold
	/// piles, and all of them pinging on the cadence is a wall of noise (heard in play as
	/// "something following me around"). There chests and loot do not ping on their own;
	/// the locator key answers with ONE chest ping at the nearest uncollected treasure,
	/// the enemy locator's press-per-answer grammar, and silence means nothing left to
	/// collect. The exit keeps its cadence - it is the room's goal and there is only one.
	///
	/// A chest or exit pings from its walk-in trigger collider, not the component's
	/// transform: in the trap rooms the component sits on a controller object away from the
	/// door (heard in play as an exit tone that circled the player), and the trigger volume
	/// is the spot the game itself completes on. The collider reference is resolved at scan
	/// time and its live bounds read at ping time; a component with no findable trigger
	/// falls back to its transform, with the resolution logged either way as recon.
	///
	/// Discovery is a periodic <c>FindObjectsOfType</c> scan: the components have no
	/// registry, no Start or OnEnable to patch, and they appear mid-level (a chest's
	/// enable-on-complete list can activate the exit or more of the gauntlet; kills drop
	/// loot), so only a rescan sees them. The scan returns active objects only, which is the
	/// right filter: an exit the level has not yet revealed does not exist for the player
	/// either. The court trigger needs no scan (a public static singleton) and pings only
	/// while its <c>IsActive</c> is true - the spawner arms it (and its collider) only once
	/// it is the last active spawner with no living AI, so an earlier ping would lure the
	/// player onto a circle that does nothing yet. All found components are live references
	/// re-read every frame (the acceptable cache); IsComplete, IsActive, activity, and
	/// position are never copied.
	/// </summary>
	internal sealed class ObjectBeacons {
		private const float ScanInterval = 1f;

		private bool _ready;
		// Each loaded sample's authored duration in seconds, keyed by sample key. Doubles as
		// the loaded set: a missing file degrades that one beacon (logged at load) instead of
		// asking the backend for an unregistered key every ping. The duration schedules the
		// next ping a fixed gap after this one's sound ends.
		private readonly Dictionary<string, float> _clips = new Dictionary<string, float>();

		// A scanned chest or exit with its resolved walk-in trigger (null when none was
		// found, in which case the component's transform stands in).
		private struct BeaconEntry<T> where T : Component {
			public T Object;
			public Collider Trigger;
		}

		private readonly List<BeaconEntry<TrapChest>> _chests = new List<BeaconEntry<TrapChest>>();
		private readonly List<BeaconEntry<TrapExit>> _exits = new List<BeaconEntry<TrapExit>>();
		private Loot[] _loot = new Loot[0];
		private bool _inLevel;
		private float _nextScan;
		private float _nextChestPing;
		private float _nextExitPing;
		// Last logged counts so the discovery line fires only on change. Doubles as recon:
		// a level with a visible exit but a zero count here means an exit type the scan
		// does not cover.
		private int _lastChests = -1;
		private int _lastExits = -1;
		private int _lastLoot = -1;
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
				if (!TreasureOnDemand() && _clips.TryGetValue(BeaconComposer.ChestKey, out float chestClip)) {
					for (int i = 0; i < _chests.Count; i++) {
						TrapChest chest = _chests[i].Object;
						if (chest == null || chest.IsComplete || !chest.gameObject.activeInHierarchy) continue;
						if (Ping(BeaconComposer.ChestKey, BeaconPosition(chest, _chests[i].Trigger), frame, out float pitch)) {
							float next = BeaconComposer.NextPingTime(now, chestClip, pitch);
							if (next > _nextChestPing) _nextChestPing = next;
						}
					}
					// Loose pickups ride the chest cadence with the chest sample: same walk-in
					// collection, same message. A collected pile is destroyed (or pool-freed,
					// which the activity check sees), so liveness is just these two checks.
					for (int i = 0; i < _loot.Length; i++) {
						Loot loot = _loot[i];
						if (loot == null || !loot.gameObject.activeInHierarchy) continue;
						if (Ping(BeaconComposer.ChestKey, loot.transform.position, frame, out float pitch)) {
							float next = BeaconComposer.NextPingTime(now, chestClip, pitch);
							if (next > _nextChestPing) _nextChestPing = next;
						}
					}
				}
			}

			if (now >= _nextExitPing) {
				_nextExitPing = now + BeaconComposer.PingGap;
				if (_clips.TryGetValue(BeaconComposer.ExitKey, out float exitClip)) {
					for (int i = 0; i < _exits.Count; i++) {
						TrapExit exit = _exits[i].Object;
						if (exit == null || exit.IsComplete || !exit.gameObject.activeInHierarchy) continue;
						if (Ping(BeaconComposer.ExitKey, BeaconPosition(exit, _exits[i].Trigger), frame, out float pitch)) {
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

		/// <summary>
		/// The treasure locator, from the locator key: one chest ping at the nearest
		/// uncollected chest or pickup. Only live in a trap room, where treasure does not
		/// ping on its own; everywhere else the press is inert (loot pings on its cadence
		/// there) and the key keeps its enemy-locator meaning. Silence on a press means
		/// nothing left to collect.
		/// </summary>
		public void TriggerLocate() {
			if (!_ready || !_clips.ContainsKey(BeaconComposer.ChestKey)) return;
			if (!TreasureOnDemand()) return;
			if (CombatManager.Instance == null || DealerQte.IsActive
					|| !CombatFrame.TryGet(out CombatFrame frame)) return;

			bool found = false;
			Vector3 nearest = default(Vector3);
			float nearestSqr = float.MaxValue;
			for (int i = 0; i < _chests.Count; i++) {
				TrapChest chest = _chests[i].Object;
				if (chest == null || chest.IsComplete || !chest.gameObject.activeInHierarchy) continue;
				Vector3 position = BeaconPosition(chest, _chests[i].Trigger);
				float sqr = (position - frame.Origin).sqrMagnitude;
				if (sqr >= nearestSqr) continue;
				found = true;
				nearest = position;
				nearestSqr = sqr;
			}
			for (int i = 0; i < _loot.Length; i++) {
				Loot loot = _loot[i];
				if (loot == null || !loot.gameObject.activeInHierarchy) continue;
				Vector3 position = loot.transform.position;
				float sqr = (position - frame.Origin).sqrMagnitude;
				if (sqr >= nearestSqr) continue;
				found = true;
				nearest = position;
				nearestSqr = sqr;
			}

			if (!found) {
				Log.Debug("treasure locator: nothing left to collect");
				return;
			}
			Ping(BeaconComposer.ChestKey, nearest, frame, out _);
		}

		// A trap room: the game wired a TrapExit as the level's completion condition.
		// Decided once at the encounter's start game-side, so it is stable for the level;
		// re-read live here per the no-caching rule.
		private static bool TreasureOnDemand() {
			CombatEncounter encounter = CombatEncounter.Instance;
			return encounter != null && encounter.HasTrapExit;
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

		// Where the beacon sounds from: the walk-in trigger's live bounds center. A disabled
		// trigger has no valid bounds, so its transform stands in; no trigger at all falls
		// back to the component's own transform (logged at scan).
		private static Vector3 BeaconPosition(Component owner, Collider trigger) {
			if (trigger == null) return owner.transform.position;
			if (trigger.enabled && trigger.gameObject.activeInHierarchy) return trigger.bounds.center;
			return trigger.transform.position;
		}

		// The trigger collider whose OnTriggerEnter the component completes on: one on its
		// own object in the classic layout, otherwise the first trigger in its children that
		// no nested trap-system component owns (a trap-room controller object can have the
		// whole gauntlet, traps and all, beneath it).
		private static Collider FindWalkInTrigger(Component owner) {
			Collider[] own = owner.GetComponents<Collider>();
			for (int i = 0; i < own.Length; i++)
				if (own[i].isTrigger) return own[i];

			Collider[] children = owner.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < children.Length; i++) {
				Collider collider = children[i];
				if (!collider.isTrigger || collider.gameObject == owner.gameObject) continue;
				if (!OwnedByNestedComponent(collider.transform, owner)) return collider;
			}

			return own.Length > 0 ? own[0] : null;
		}

		// True when another trap-system component sits between the collider and the owner:
		// that collider is the nested component's volume, not the owner's walk-in trigger.
		private static bool OwnedByNestedComponent(Transform collider, Component owner) {
			for (Transform node = collider; node != null && node != owner.transform; node = node.parent) {
				if (node.GetComponent<Trap>() != null || node.GetComponent<CombatApplicant>() != null
						|| node.GetComponent<TrapChest>() != null || node.GetComponent<TrapExit>() != null
						|| node.GetComponent<Loot>() != null) return true;
			}
			return false;
		}

		private void Scan() {
			TrapChest[] chests = UnityEngine.Object.FindObjectsOfType<TrapChest>();
			TrapExit[] exits = UnityEngine.Object.FindObjectsOfType<TrapExit>();
			_loot = UnityEngine.Object.FindObjectsOfType<Loot>();

			_chests.Clear();
			for (int i = 0; i < chests.Length; i++)
				_chests.Add(new BeaconEntry<TrapChest> { Object = chests[i], Trigger = FindWalkInTrigger(chests[i]) });
			_exits.Clear();
			for (int i = 0; i < exits.Length; i++)
				_exits.Add(new BeaconEntry<TrapExit> { Object = exits[i], Trigger = FindWalkInTrigger(exits[i]) });

			// Loot churns with every drop and pickup, so it only refreshes the count line;
			// the per-object resolution lines re-log only when the chest/exit set changes.
			bool setChanged = chests.Length != _lastChests || exits.Length != _lastExits;
			if (setChanged || _loot.Length != _lastLoot) {
				Log.Info("beacons: " + chests.Length + " chest(s), " + exits.Length + " exit(s), "
					+ _loot.Length + " loot");
				if (setChanged) {
					// Where each chest and exit will sound from, as recon for a beacon heard
					// in the wrong place: the component transform versus the resolved trigger.
					for (int i = 0; i < _chests.Count; i++) LogResolution("chest", _chests[i].Object, _chests[i].Trigger);
					for (int i = 0; i < _exits.Count; i++) LogResolution("exit", _exits[i].Object, _exits[i].Trigger);
				}
				_lastChests = chests.Length;
				_lastExits = exits.Length;
				_lastLoot = _loot.Length;
			}
		}

		private static void LogResolution(string kind, Component owner, Collider trigger) {
			Vector3 at = BeaconPosition(owner, trigger);
			Vector3 component = owner.transform.position;
			Log.Info("beacon " + kind + " '" + owner.name + "': "
				+ (trigger == null
					? "no walk-in trigger found, pinging at its transform"
					: "pinging at trigger '" + trigger.name + "'")
				+ " (" + at.x.ToString("F1") + ", " + at.z.ToString("F1") + "); component at ("
				+ component.x.ToString("F1") + ", " + component.z.ToString("F1") + ")");
		}

		private void Reset() {
			if (!_inLevel) return;
			_inLevel = false;
			_chests.Clear();
			_exits.Clear();
			_loot = new Loot[0];
			_lastChests = -1;
			_lastExits = -1;
			_lastLoot = -1;
			_courtWasActive = false;
		}
	}
}
