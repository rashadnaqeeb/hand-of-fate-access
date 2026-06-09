using System;
using System.Collections.Generic;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The combat attack-telegraph cue: when an enemy attack's parry window opens (the moment
	/// the game flashes its own block-or-dodge indicator and slows the windup animation), a
	/// positioned block-or-dodge sample tells the player a strike is coming, where it comes
	/// from, and how to react. One cue per attack: the window opens as the windup begins and
	/// the whole slowed windup is the reaction time, so there is no separate, later "act now"
	/// moment to cue (verified live; the strike's hit event lands too late to react to).
	///
	/// The moment is caught by Harmony hooks on <c>CombatUtils.StartMeleeEffect</c>/
	/// <c>StartRangedEffect</c>, which every enemy attack calls as its window opens. Per the
	/// project's hook discipline those hooks only record the event and the attacker's world
	/// position into the queue here; this pump, run from the update loop, drains the queue and
	/// plays each cue. Recording the position is recording the event, not caching live state: a
	/// telegraph is anchored where the attacker stood when the swing began, and a one-shot fired
	/// a frame later belongs at that spot, not wherever the attacker has since moved.
	///
	/// Core's <see cref="AttackCueComposer"/> decides the sample and its pan/pitch/volume from the
	/// attacker's offset; this reads that offset off the live combat frame and owns the playback.
	/// </summary>
	internal sealed class AttackCues {
		private struct Pending {
			public string Key;
			public Vector3 Position;
		}

		// Single Unity thread: the hooks fire during the game's combat update and this pump runs
		// from the same update loop, so the queue needs no synchronization. Bounded in practice by
		// the handful of attacks that can commit in one frame; the pump fully drains it each frame.
		private static readonly Queue<Pending> _pending = new Queue<Pending>();

		// Sample keys that actually loaded, so the pump drops a cue whose sample is missing rather
		// than asking the backend to play an unregistered key every frame (which would log spam).
		private readonly HashSet<string> _loaded = new HashSet<string>();

		// Time.time when a cue last actually played, -1 before the first. Read by the damage
		// tripwire to correlate each hit the player takes with the most recent telegraph: a hit
		// long after the last cue (or before any) is an uncued damage source to go fix.
		private static float _lastCueTime = -1f;
		public static float LastCueTime => _lastCueTime;

		/// <summary>Record an attack's cue (block when <paramref name="blockable"/>, else dodge)
		/// at the attacker's world position. Called from the effect-start hooks.</summary>
		public static void RecordAction(bool blockable, Vector3 position) {
			_pending.Enqueue(new Pending { Key = AttackCueComposer.ActionKey(blockable), Position = position });
		}

		/// <summary>
		/// Load the two telegraph samples from <paramref name="pluginDir"/>/sounds. A sample
		/// that fails to load leaves that cue silent (logged), so a missing file degrades one cue
		/// rather than the feature.
		/// </summary>
		public void Initialize(string pluginDir) {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; attack cues disabled");
				return;
			}
			string soundsDir = Path.Combine(pluginDir, "sounds");
			LoadSample(soundsDir, AttackCueComposer.BlockKey);
			LoadSample(soundsDir, AttackCueComposer.DodgeKey);
		}

		private void LoadSample(string soundsDir, string key) {
			string path = Path.Combine(soundsDir, key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(key, pcm, channels, sampleRate);
				_loaded.Add(key);
			} catch (Exception ex) {
				Log.Error("attack cue '" + key + "' failed to load from " + path + ": " + ex);
			}
		}

		/// <summary>
		/// Play any queued cues for this frame. Outside an active combat encounter (or before
		/// audio is up) the queue is dropped, so a cue recorded on a fight's last frame never
		/// fires stale into the post-combat walk. The encounter gate matters: the combat frame
		/// alone is live for all of level play, not just fights.
		/// </summary>
		public void Pump() {
			if (!AudioEngine.IsAvailable || CombatEncounter.Instance == null
					|| !CombatFrame.TryGet(out CombatFrame frame)) {
				_pending.Clear();
				return;
			}

			while (_pending.Count > 0) {
				Pending cue = _pending.Dequeue();
				if (!_loaded.Contains(cue.Key)) continue;
				Vector3 rel = cue.Position - frame.Origin;
				SoundParams sp = AttackCueComposer.Compose(
					Vector3.Dot(rel, frame.Right), Vector3.Dot(rel, frame.Forward));
				AudioEngine.PlayOneShot(cue.Key, sp);
				_lastCueTime = Time.time;
			}
		}
	}
}
