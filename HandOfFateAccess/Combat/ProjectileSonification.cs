using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Sonifies every hostile projectile in a fight: each gets its own looping voice whose
	/// pan, pitch, and volume track where the projectile sits relative to the player, so a
	/// player who cannot see the arena can hear a projectile's bearing (pan for east/west,
	/// pitch for north/south), its proximity (volume), and where it is heading as those move
	/// frame to frame. Only enemy-team projectiles are voiced: the player's own shots and
	/// reflections are not threats and would only clutter the field.
	///
	/// The loop is synthesized per voice by a <see cref="ProjectileVoicePool"/> rather than
	/// played from a sample, so the north/south pitch can drop without the tumble dragging.
	/// Core's <see cref="ProjectileSonifier"/> decides the three numbers from each projectile's
	/// offset; this reads that offset off the live transform and owns the voices.
	///
	/// The voice map is keyed by the live projectile object: one entering CombatManager's
	/// list gets a voice, one leaving it (expired or impacted) has its voice released. Holding
	/// the projectile reference and its voice is the mod's own audio state, not game state;
	/// nothing the player hears is stored, every pan, pitch, and volume is recomputed from the
	/// live position each frame. Voices are capped at the pool size, nearest projectiles
	/// first, so the busiest volley voices the closest threats.
	/// </summary>
	internal sealed class ProjectileSonification {
		// Headroom for busy maps: many archers or a boss spray can put a lot of projectiles
		// up at once. The pool is sized to this, and the nearest-first cap matches it.
		private const int MaxVoices = 32;

		private ProjectileVoicePool _pool;
		private readonly Dictionary<CombatProxyProjectile, ProjectileVoice> _voices =
			new Dictionary<CombatProxyProjectile, ProjectileVoice>();
		// Per-frame scratch, reused to avoid allocating in the pump: the chosen projectiles
		// this frame (ordered, for the nearest-first cap), the same set for O(1) membership
		// tests in the cleanup pass, and the keys whose voices to drop.
		private readonly List<CombatProxyProjectile> _live = new List<CombatProxyProjectile>();
		private readonly HashSet<CombatProxyProjectile> _liveSet = new HashSet<CombatProxyProjectile>();
		private readonly List<CombatProxyProjectile> _gone = new List<CombatProxyProjectile>();
		// Last logged (total live, enemy-voiced) counts, so the diagnostic line fires only
		// when the field changes rather than every frame. -1 forces a log on the first change.
		private int _lastTotal = -1;
		private int _lastEnemy = -1;

		/// <summary>The projectile voice pool, shared with the F9 test sweep. Null until built.</summary>
		public ProjectileVoicePool Voices => _pool;

		/// <summary>
		/// Stand up the synthesized-voice pool. Skipped (logged) when the audio backend never
		/// came up, so a headless or audio-less run degrades quietly rather than crashing.
		/// </summary>
		public void Initialize() {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; projectile sonification disabled");
				return;
			}
			_pool = new ProjectileVoicePool(MaxVoices);
			Log.Debug("projectile sonification ready");
		}

		/// <summary>
		/// Drive the projectile voices for this frame. Outside combat (or before the loop
		/// loaded) every voice is released; inside combat each live enemy projectile's voice
		/// is started or re-aimed and voices for projectiles that have left the field are cut.
		/// </summary>
		public void Pump() {
			if (_pool == null) return;

			if (CombatManager.Instance == null || !CombatFrame.TryGet(out CombatFrame frame)) {
				StopAll();
				return;
			}

			List<CombatProxyProjectile> projectiles = CombatManager.Instance.Projectiles;

			// Gather the live enemy projectiles. Team is read off the projectile's own
			// Targetable, which the game stamps with the firer's team on engage, so the
			// player's own shots and reflections stay silent.
			_live.Clear();
			for (int i = 0; i < projectiles.Count; i++) {
				CombatProxyProjectile proj = projectiles[i];
				if (proj == null) continue;
				// A reflected projectile keeps its enemy team but now flies at the enemy, so it
				// is no longer a threat to the player; voicing it would be wrong information.
				if (proj.IsReflected) continue;
				Targetable targetable = proj.GetComponent<Targetable>();
				if (targetable == null || targetable.Team != TeamType.Enemy) continue;
				_live.Add(proj);
			}

			// Diagnostic: how many projectiles the game has live versus how many we voice after
			// the enemy filter. A total above zero with enemy zero would mean the team filter is
			// rejecting real threats. Logged only when the counts change.
			if (projectiles.Count != _lastTotal || _live.Count != _lastEnemy) {
				Log.Debug("projectiles: " + projectiles.Count + " live, " + _live.Count + " enemy voiced");
				_lastTotal = projectiles.Count;
				_lastEnemy = _live.Count;
			}

			// Keep only the nearest MaxVoices so the pool is never exhausted.
			if (_live.Count > MaxVoices) {
				Vector3 origin = frame.Origin;
				_live.Sort((a, b) =>
					(a.transform.position - origin).sqrMagnitude.CompareTo(
						(b.transform.position - origin).sqrMagnitude));
				_live.RemoveRange(MaxVoices, _live.Count - MaxVoices);
			}
			_liveSet.Clear();
			for (int i = 0; i < _live.Count; i++) _liveSet.Add(_live[i]);

			for (int i = 0; i < _live.Count; i++) {
				CombatProxyProjectile proj = _live[i];
				Vector3 rel = proj.transform.position - frame.Origin;
				SoundParams sp = ProjectileSonifier.Compose(
					Vector3.Dot(rel, frame.Right), Vector3.Dot(rel, frame.Forward));

				ProjectileVoice voice;
				if (_voices.TryGetValue(proj, out voice)) {
					voice.SetParams(sp.Pitch, sp.Pan, sp.Volume);
				} else {
					voice = _pool.Acquire();
					if (voice != null) {
						voice.Play(sp.Pitch, sp.Pan, sp.Volume);
						_voices[proj] = voice;
					}
				}
			}

			// Cut voices for projectiles that have left the field or fell outside the cap.
			foreach (KeyValuePair<CombatProxyProjectile, ProjectileVoice> kv in _voices) {
				if (kv.Key == null || !_liveSet.Contains(kv.Key)) _gone.Add(kv.Key);
			}
			for (int i = 0; i < _gone.Count; i++) {
				_pool.Release(_voices[_gone[i]]);
				_voices.Remove(_gone[i]);
			}
			_gone.Clear();
		}

		private void StopAll() {
			_lastTotal = -1;
			_lastEnemy = -1;
			if (_voices.Count == 0) return;
			foreach (KeyValuePair<CombatProxyProjectile, ProjectileVoice> kv in _voices)
				_pool.Release(kv.Value);
			_voices.Clear();
		}
	}
}
