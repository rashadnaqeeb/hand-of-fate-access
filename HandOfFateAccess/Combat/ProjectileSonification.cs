using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using HarmonyLib;
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
	///
	/// Mover hazards ride the same path: lobs (arcing bombs) and lightning heads are damage
	/// proxies that fly like projectiles but never enter CombatManager's list, so their engage
	/// hooks record them here and the pump voices them identically. From the player's side a
	/// mover IS a projectile: it is incoming, its bearing and closing distance are what matter,
	/// and the reaction is the same dodge, so it earns the same sound rather than a new one. A
	/// mover's voice stops when it expires (a lob that landed lingers as particles while its
	/// private expiring flag is set), not when its object is finally destroyed.
	/// </summary>
	internal sealed class ProjectileSonification {
		// Headroom for busy maps: many archers or a boss spray can put a lot of projectiles
		// up at once. The pool is sized to this, and the nearest-first cap matches it.
		private const int MaxVoices = 32;

		private ProjectileVoicePool _pool;
		private readonly Dictionary<CombatProxy, ProjectileVoice> _voices =
			new Dictionary<CombatProxy, ProjectileVoice>();
		// Per-frame scratch, reused to avoid allocating in the pump: the chosen projectiles
		// this frame (ordered, for the nearest-first cap), the same set for O(1) membership
		// tests in the cleanup pass, and the keys whose voices to drop.
		private readonly List<CombatProxy> _live = new List<CombatProxy>();
		private readonly HashSet<CombatProxy> _liveSet = new HashSet<CombatProxy>();
		private readonly List<CombatProxy> _gone = new List<CombatProxy>();
		// Last logged (total live, voiced, movers) counts, so the diagnostic line fires only
		// when the field changes rather than every frame. -1 forces a log on the first change.
		private int _lastTotal = -1;
		private int _lastVoiced = -1;
		private int _lastMovers = -1;

		// The live movers, recorded by the lob/lightning engage hooks and pruned here as they
		// expire or are destroyed. Holding the live proxy reference is the acceptable cache:
		// every parameter the player hears is recomputed from its transform each frame. Single
		// Unity thread (the hooks fire during the game's update, this pump from ours), so no
		// synchronization, same as the AttackCues queue.
		private static readonly List<CombatProxy> _movers = new List<CombatProxy>();

		// The movers' private expiring flags: a lob that landed lingers for seconds as blast
		// particles with this set, and voicing that corpse would tell the player a threat is
		// still flying. Resolved once per type; a game-side rename crashes the pump loudly (and
		// is caught in testing) rather than silently voicing spent hazards.
		private static readonly FieldInfo LobExpiring =
			AccessTools.Field(typeof(CombatProxyLob), "m_isExpiring");
		private static readonly FieldInfo LightningExpiring =
			AccessTools.Field(typeof(CombatProxyLightning), "m_isExpiring");

		/// <summary>Track a mover hazard from its engage hook, which has already filtered for
		/// hostile sources; the pump voices it like a projectile until it expires or is
		/// destroyed.</summary>
		public static void RecordMover(CombatProxy mover) {
			if (!_movers.Contains(mover)) _movers.Add(mover);
		}

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

			// The Dealer's missile quick-time counts as outside: the player is teleported and
			// the camera overridden, so a bearing projected there would mislead.
			if (CombatManager.Instance == null || DealerQte.IsActive
					|| !CombatFrame.TryGet(out CombatFrame frame)) {
				StopAll();
				return;
			}

			List<CombatProxyProjectile> projectiles = CombatManager.Instance.Projectiles;

			// Gather the projectiles fired by a hostile source. Team is read off the firer (the
			// effect's Source targetable), not the projectile object: a projectile rarely carries
			// its own Targetable, so reading it off the projectile leaves every shot team None and
			// silences the field. The firer's team is unchanged after the player reflects the
			// shot, so reflected shots are gathered here too and voiced with a faster tumble (see
			// the voice loop below) rather than dropped, letting the player track a bounced-back
			// shot to the enemy while still telling it apart from an incoming threat.
			_live.Clear();
			for (int i = 0; i < projectiles.Count; i++) {
				CombatProxyProjectile proj = projectiles[i];
				if (proj == null) continue;
				if (!Hostility.ThreatensPlayer(proj.Effect.Source)) continue;
				_live.Add(proj);
			}

			// Movers join the same field. Dead ones drop out of the registry here: a disengaged
			// proxy is destroyed (compares null), an expired one has landed and is only
			// particles. Hostility was decided once at the engage hook, so everything left is
			// voiced.
			int moverCount = 0;
			for (int i = _movers.Count - 1; i >= 0; i--) {
				CombatProxy mover = _movers[i];
				if (mover == null || HasExpired(mover)) {
					_movers.RemoveAt(i);
					continue;
				}
				_live.Add(mover);
				moverCount++;
			}

			// Diagnostic: how many projectiles the game has live versus how many we voice after
			// the enemy filter (movers counted separately). A total above zero with voiced zero
			// would mean the team filter is rejecting real threats. Logged only when the counts
			// change.
			if (projectiles.Count != _lastTotal || _live.Count != _lastVoiced || moverCount != _lastMovers) {
				Log.Debug("projectiles: " + projectiles.Count + " live, " + _live.Count
					+ " voiced (" + moverCount + " movers)");
				_lastTotal = projectiles.Count;
				_lastVoiced = _live.Count;
				_lastMovers = moverCount;
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
				CombatProxy proj = _live[i];
				frame.Project(proj.transform.position, out float right, out float forward);
				SoundParams sp = ProjectileSonifier.Compose(right, forward);

				// Only true projectiles can be reflected; a mover always tumbles at threat speed.
				bool reflected = proj is CombatProxyProjectile p && p.IsReflected;
				ProjectileVoice voice;
				if (_voices.TryGetValue(proj, out voice)) {
					voice.SetParams(sp.Pitch, sp.Pan, sp.Volume, reflected);
				} else {
					voice = _pool.Acquire();
					if (voice != null) {
						voice.Play(sp.Pitch, sp.Pan, sp.Volume, reflected);
						_voices[proj] = voice;
					}
				}
			}

			// Cut voices for projectiles that have left the field or fell outside the cap.
			foreach (KeyValuePair<CombatProxy, ProjectileVoice> kv in _voices) {
				if (kv.Key == null || !_liveSet.Contains(kv.Key)) _gone.Add(kv.Key);
			}
			for (int i = 0; i < _gone.Count; i++) {
				_pool.Release(_voices[_gone[i]]);
				_voices.Remove(_gone[i]);
			}
			_gone.Clear();
		}

		// Whether a mover has stopped being a threat while its object still exists. Only the
		// two recorded mover types reach here; a third would throw on the wrong field, which
		// is the visible failure we want over a silently wrong flag.
		private static bool HasExpired(CombatProxy mover) {
			FieldInfo field = mover is CombatProxyLob ? LobExpiring : LightningExpiring;
			return (bool)field.GetValue(mover);
		}

		private void StopAll() {
			_lastTotal = -1;
			_lastVoiced = -1;
			_lastMovers = -1;
			_movers.Clear();
			if (_voices.Count == 0) return;
			foreach (KeyValuePair<CombatProxy, ProjectileVoice> kv in _voices)
				_pool.Release(kv.Value);
			_voices.Clear();
		}
	}
}
