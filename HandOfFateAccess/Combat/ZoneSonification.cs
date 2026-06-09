using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Voices the zone hazards: ground areas (fire patches, blast zones, proximity mines)
	/// that a sighted player reads as a decal on the floor. Each near zone gets a looping
	/// voice placed at its nearest dangerous point, so "move away from the sound" is always
	/// the correct escape; the loop itself carries the state (arming throb, active buzz,
	/// inside rattle). Core's <see cref="ZoneSonifier"/> decides everything heard and is
	/// unit-tested; this adapter only extracts each area's live shape and drives the voices.
	///
	/// No hooks: the game keeps its own public registry (<c>CombatProxyArea.AllAreas</c>),
	/// polled here each frame exactly like the projectile list. The danger bound is read off
	/// the area's live collider bounds, which also tracks the grow-in of expanding zones.
	/// Holding a voice per live area object is the mod's own audio state; every shape and
	/// state input is re-read from the game each frame.
	///
	/// Approximations, deliberate: cones (angle-limited areas) are voiced as full discs,
	/// over-warning rather than risking an unheard arc (the engage recon log records the
	/// real angle for later refinement); a proximity mine is voiced as an active zone from
	/// birth. The mine's arming delay does defer its fuse (entrants during the window are
	/// queued and the fuse starts once it elapses), but a queued entrant still trips it
	/// with no safe exit after, so the player should never hear a mine as "safe for now".
	/// </summary>
	internal sealed class ZoneSonification {
		// Voices for the nearest zones only: arenas can accumulate hazards, and three
		// simultaneous loops is the most a player can usefully track on top of the wind,
		// projectiles, and cues. Silence past the falloff is itself information.
		private const int MaxZoneVoices = 3;
		private const int RenderSampleRate = 44100;

		private struct ZoneVoice {
			// Fully qualified: the game's Assembly-CSharp has its own global Voice type.
			public HandOfFateAccess.Audio.Voice Voice;
			public string Clip;
		}

		private struct LiveZone {
			public CombatProxyArea Area;
			public ZoneCue Cue;
		}

		private bool _ready;
		private readonly Dictionary<CombatProxyArea, ZoneVoice> _voices =
			new Dictionary<CombatProxyArea, ZoneVoice>();
		// Per-frame scratch, reused to avoid allocating in the pump.
		private readonly List<LiveZone> _live = new List<LiveZone>();
		private readonly HashSet<CombatProxyArea> _keep = new HashSet<CombatProxyArea>();
		private readonly List<CombatProxyArea> _gone = new List<CombatProxyArea>();
		// Last logged counts so the diagnostic line fires only on change.
		private int _lastLive = -1;
		private int _lastAudible = -1;

		// The area's private shape and lifecycle fields, resolved once. m_time is the engage
		// Time.time; with m_activationDelay it gives the arming window. m_isExpiring marks an
		// area that has stopped damaging and only awaits its particles, which must fall
		// silent. A game-side rename crashes the pump loudly rather than voicing wrong state.
		private static readonly FieldInfo AreaTime = AccessTools.Field(typeof(CombatProxyArea), "m_time");
		private static readonly FieldInfo AreaDelay = AccessTools.Field(typeof(CombatProxyArea), "m_activationDelay");
		private static readonly FieldInfo AreaInner = AccessTools.Field(typeof(CombatProxyArea), "m_innerRadius");
		private static readonly FieldInfo AreaMine = AccessTools.Field(typeof(CombatProxyArea), "m_isProximityMine");
		private static readonly FieldInfo AreaExpiring = AccessTools.Field(typeof(CombatProxyArea), "m_isExpiring");
		// The authored full radius of a growing zone (the game stashes the collider's local
		// radius here at engage, then inflates the collider from zero over the grow time).
		// Zero for zones that do not grow.
		private static readonly FieldInfo AreaFullSize = AccessTools.Field(typeof(CombatProxyArea), "m_colliderSize");

		/// <summary>Render and register the three zone loops. Skipped (logged) when the audio
		/// backend never came up.</summary>
		public void Initialize() {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; zone sonification disabled");
				return;
			}
			AudioEngine.Register(ZoneSynth.ArmingKey, ZoneSynth.RenderArming(RenderSampleRate), 1, RenderSampleRate);
			AudioEngine.Register(ZoneSynth.ActiveKey, ZoneSynth.RenderActive(RenderSampleRate), 1, RenderSampleRate);
			AudioEngine.Register(ZoneSynth.InsideKey, ZoneSynth.RenderInside(RenderSampleRate), 1, RenderSampleRate);
			_ready = true;
			Log.Debug("zone sonification ready");
		}

		/// <summary>
		/// Drive the zone voices for this frame. Outside combat every voice stops; inside,
		/// each live hostile area is composed, the nearest are voiced, and voices for zones
		/// that ended or fell outside the cap are released.
		/// </summary>
		public void Pump() {
			if (!_ready) return;

			if (CombatManager.Instance == null || !CombatFrame.TryGet(out CombatFrame frame)) {
				StopAll();
				return;
			}

			List<CombatProxyArea> areas = CombatProxyArea.AllAreas;
			_live.Clear();
			for (int i = 0; i < areas.Count; i++) {
				CombatProxyArea area = areas[i];
				if (area == null || (bool)AreaExpiring.GetValue(area)) continue;
				Targetable source = area.Effect.Source;
				if (source == null || source.Team != TeamType.Enemy) continue;

				// The danger bound, live off the collider (bounds are world space, so scale
				// is folded in) - but a growing zone's collider starts at radius ZERO and
				// inflates over its grow time, which would voice it as a point and under-warn
				// its footprint. The authored full radius is floored in so the player hears
				// the disc the zone is about to become: the over-warn is the safe direction.
				Collider collider = area.GetComponent<Collider>();
				if (collider == null) continue;
				Bounds bounds = collider.bounds;
				float outer = Mathf.Max(bounds.extents.x, bounds.extents.z);
				float fullSize = (float)AreaFullSize.GetValue(area) * area.transform.lossyScale.x;
				if (fullSize > outer) outer = fullSize;

				bool mine = (bool)AreaMine.GetValue(area);
				float elapsed = Time.time - (float)AreaTime.GetValue(area);
				ZonePhase phase = !mine && elapsed < (float)AreaDelay.GetValue(area)
					? ZonePhase.Arming : ZonePhase.Active;

				Vector3 rel = area.transform.position - frame.Origin;
				ZoneCue cue = ZoneSonifier.Compose(
					Vector3.Dot(rel, frame.Right), Vector3.Dot(rel, frame.Forward),
					outer, (float)AreaInner.GetValue(area), phase);
				if (!cue.Audible) continue;
				_live.Add(new LiveZone { Area = area, Cue = cue });
			}

			// Diagnostic: how many areas the game has live versus how many are near enough to
			// voice. Logged only when the counts change.
			if (areas.Count != _lastLive || _live.Count != _lastAudible) {
				Log.Debug("zones: " + areas.Count + " live, " + _live.Count + " audible");
				_lastLive = areas.Count;
				_lastAudible = _live.Count;
			}

			// Keep only the nearest MaxZoneVoices; an inside zone has distance zero, so it
			// always survives the cut.
			if (_live.Count > MaxZoneVoices) {
				_live.Sort((a, b) => a.Cue.Distance.CompareTo(b.Cue.Distance));
				_live.RemoveRange(MaxZoneVoices, _live.Count - MaxZoneVoices);
			}

			_keep.Clear();
			for (int i = 0; i < _live.Count; i++) {
				LiveZone zone = _live[i];
				_keep.Add(zone.Area);
				ZoneVoice voice;
				if (_voices.TryGetValue(zone.Area, out voice)) {
					if (voice.Clip != zone.Cue.ClipKey) {
						// State changed (armed, or the player crossed the edge): swap loops.
						// The restart is an event the player should notice, not a glitch.
						AudioEngine.Stop(voice.Voice);
						voice.Voice = AudioEngine.Play(zone.Cue.ClipKey, zone.Cue.Params, true);
						voice.Clip = zone.Cue.ClipKey;
						_voices[zone.Area] = voice;
					} else {
						AudioEngine.Update(voice.Voice, zone.Cue.Params);
					}
				} else {
					HandOfFateAccess.Audio.Voice started = AudioEngine.Play(zone.Cue.ClipKey, zone.Cue.Params, true);
					if (started.IsValid)
						_voices[zone.Area] = new ZoneVoice { Voice = started, Clip = zone.Cue.ClipKey };
				}
			}

			// Release voices for zones that ended, went silent, or fell outside the cap.
			foreach (KeyValuePair<CombatProxyArea, ZoneVoice> kv in _voices) {
				if (kv.Key == null || !_keep.Contains(kv.Key)) _gone.Add(kv.Key);
			}
			for (int i = 0; i < _gone.Count; i++) {
				AudioEngine.Stop(_voices[_gone[i]].Voice);
				_voices.Remove(_gone[i]);
			}
			_gone.Clear();
		}

		private void StopAll() {
			_lastLive = -1;
			_lastAudible = -1;
			if (_voices.Count == 0) return;
			foreach (KeyValuePair<CombatProxyArea, ZoneVoice> kv in _voices)
				AudioEngine.Stop(kv.Value.Voice);
			_voices.Clear();
		}
	}
}
