using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Voices the zone hazards: ground areas (fire patches, blast zones, proximity mines)
	/// that a sighted player reads as a decal on the floor, and the level's traps (swinging
	/// blades, spike floors), which are the same message in a different system: a place with
	/// a state. Each near hazard gets a looping voice placed at its nearest dangerous point,
	/// so "move away from the sound" is always the correct escape; the loop itself carries
	/// the state (primed pulse, arming throb, active buzz, inside rattle). Core's
	/// <see cref="ZoneSonifier"/> decides everything heard and is unit-tested; this adapter
	/// only extracts each hazard's live shape and drives the voices.
	///
	/// No hooks: the game keeps its own public registry (<c>CombatProxyArea.AllAreas</c>),
	/// polled here each frame exactly like the projectile list. The danger bound is read off
	/// the area's live collider bounds, which also tracks the grow-in of expanding zones.
	/// Holding a voice per live area object is the mod's own audio state; every shape and
	/// state input is re-read from the game each frame.
	///
	/// Traps are scene objects with no registry, so they are discovered by a
	/// <c>FindObjectsOfType</c> scan, rerun when <c>CombatEncounter.Instance</c> changes or
	/// when <c>Trap_Start_Patch</c> flags a trap coming active (a chest or exit can switch
	/// trap hierarchies on mid-level), and then read live each frame: armed is each damage
	/// trigger's apply flag, the footprint is that trigger's collider, one voice per trigger
	/// since a trap can cycle several independently. A trap's deterministic phase cycle
	/// needs no rhythm machinery of its own - the voice simply follows the armed flag, so
	/// the alternation IS the audible cycle and the player times the crossing by ear. The
	/// safe beat voices as arming (exists, damage off, leaving free), as does a trigger
	/// whose collider the cycle disabled (it cannot detect anyone, so it cannot hit, but it
	/// is not gone); a trap whose authored cycle idles on a proximity trigger instead voices
	/// its quiet time as primed, because it fires when approached and silence would read as
	/// a clear corridor.
	///
	/// Approximations, deliberate: cones (angle-limited areas) are voiced as full discs,
	/// over-warning rather than risking an unheard arc (the engage recon log records the
	/// real angle for later refinement); a proximity mine is voiced as an active zone from
	/// birth. The mine's arming delay does defer its fuse (entrants during the window are
	/// queued and the fuse starts once it elapses), but a queued entrant still trips it
	/// with no safe exit after, so the player should never hear a mine as "safe for now".
	/// A trap's footprint is its collider's axis-aligned bounds (Unity 5.3 has no exact
	/// closest-point API), which over-warns rotated boxes - the safe direction. Insideness
	/// is judged on the horizontal plane only, so a blade sweeping overhead still rattles.
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
			// The live game object the voice belongs to: a CombatProxyArea or a Trap.
			public Component Key;
			public ZoneCue Cue;
		}

		// One scanned damage trigger: the components re-read every frame. Holding these live
		// component references is the acceptable cache; armed state and shape are read off
		// them fresh. A trap can own several triggers (each phase-cycled independently), so
		// the scan emits one entry, and one voice, per trigger, not per trap.
		private struct TrapEntry {
			public CombatApplicantTrigger Applicant;
			public Collider Collider;
			// The authored cycle idles on a proximity trigger: quiet means "fires when
			// approached", not "safe", so this trap's off-beats voice as Primed.
			public bool Primed;
		}

		private bool _ready;
		private readonly Dictionary<Component, ZoneVoice> _voices =
			new Dictionary<Component, ZoneVoice>();
		// Per-frame scratch, reused to avoid allocating in the pump.
		private readonly List<LiveZone> _live = new List<LiveZone>();
		private readonly HashSet<Component> _keep = new HashSet<Component>();
		private readonly List<Component> _gone = new List<Component>();
		// The level's traps, rescanned when the encounter changes or a trap becomes active.
		private readonly List<TrapEntry> _traps = new List<TrapEntry>();
		private CombatEncounter _trapLevel;
		// Set by Trap_Start_Patch when a trap's phase coroutine is created, i.e. the trap
		// just became active. A chest or exit can switch whole trap hierarchies on
		// mid-level, which a once-per-level scan would miss: an invisible hazard.
		private static bool s_trapsChanged;
		// Last logged counts so the diagnostic line fires only on change.
		private int _lastAreas = -1;
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

		// The trap's armed/safe switch: the one boolean its authored phase cycle flips, and
		// retroactive (arming hits everyone already standing inside). Polled per frame.
		private static readonly FieldInfo TriggerArmed = AccessTools.Field(typeof(CombatApplicantTrigger), "m_applyToTargets");
		// The trap's authored phase list, read once per scan to spot proximity-triggered traps.
		private static readonly FieldInfo TrapPhases = AccessTools.Field(typeof(Trap), "m_trapPhases");

		/// <summary>Render and register the four zone loops. Skipped (logged) when the audio
		/// backend never came up.</summary>
		public void Initialize() {
			if (!AudioEngine.IsAvailable) {
				Log.Warn("audio backend unavailable; zone sonification disabled");
				return;
			}
			AudioEngine.Register(ZoneSynth.PrimedKey, ZoneSynth.RenderPrimed(RenderSampleRate), 1, RenderSampleRate);
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

				frame.Project(area.transform.position, out float right, out float forward);
				ZoneCue cue = ZoneSonifier.Compose(
					right, forward, outer, (float)AreaInner.GetValue(area), phase);
				if (!cue.Audible) continue;
				_live.Add(new LiveZone { Key = area, Cue = cue });
			}

			// Traps live for the whole level; rescan when the encounter changes or a trap
			// becomes active (the patch flag covers traps switched on mid-level, and also
			// levels that have traps but never an encounter, if any exist).
			CombatEncounter encounter = CombatEncounter.Instance;
			if (encounter != _trapLevel || s_trapsChanged) {
				_trapLevel = encounter;
				s_trapsChanged = false;
				ScanTraps();
			}
			for (int i = 0; i < _traps.Count; i++) {
				TrapEntry trap = _traps[i];
				// An inactive trigger object is genuinely absent (its trap is not running);
				// a merely disabled collider is not: the trap exists and its cycle can
				// re-enable it, so it keeps a voice, just never an armed one (a disabled
				// trigger cannot detect anyone, hence cannot hit).
				if (trap.Collider == null || !trap.Collider.gameObject.activeInHierarchy)
					continue;

				bool armed = trap.Collider.enabled && (bool)TriggerArmed.GetValue(trap.Applicant);
				ZonePhase phase = armed ? ZonePhase.Active
					: trap.Primed ? ZonePhase.Primed : ZonePhase.Arming;

				// Nearest dangerous point on the trap's bounds; insideness is horizontal only
				// (an overhead blade at the player's spot still counts). Inside, the bearing
				// retargets the bounds center so fleeing the sound exits through the near edge.
				frame.Project(trap.Collider.ClosestPointOnBounds(frame.Origin), out float trapRight, out float trapForward);
				bool inside = trapRight * trapRight + trapForward * trapForward < 0.01f;
				if (inside)
					frame.Project(trap.Collider.bounds.center, out trapRight, out trapForward);

				ZoneCue cue = ZoneSonifier.ComposePoint(trapRight, trapForward, inside, phase);
				if (!cue.Audible) continue;
				_live.Add(new LiveZone { Key = trap.Applicant, Cue = cue });
			}

			// Diagnostic: how many areas the game has live versus how many hazards are near
			// enough to voice. Logged only when the counts change.
			if (areas.Count != _lastAreas || _live.Count != _lastAudible) {
				Log.Debug("zones: " + areas.Count + " areas, " + _traps.Count + " traps, " + _live.Count + " audible");
				_lastAreas = areas.Count;
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
				_keep.Add(zone.Key);
				ZoneVoice voice;
				if (_voices.TryGetValue(zone.Key, out voice)) {
					if (voice.Clip != zone.Cue.ClipKey) {
						// State changed (armed, or the player crossed the edge): swap loops.
						// The restart is an event the player should notice, not a glitch.
						AudioEngine.Stop(voice.Voice);
						voice.Voice = AudioEngine.Play(zone.Cue.ClipKey, zone.Cue.Params, true);
						voice.Clip = zone.Cue.ClipKey;
						_voices[zone.Key] = voice;
					} else {
						AudioEngine.Update(voice.Voice, zone.Cue.Params);
					}
				} else {
					HandOfFateAccess.Audio.Voice started = AudioEngine.Play(zone.Cue.ClipKey, zone.Cue.Params, true);
					if (started.IsValid)
						_voices[zone.Key] = new ZoneVoice { Voice = started, Clip = zone.Cue.ClipKey };
				}
			}

			// Release voices for zones that ended, went silent, or fell outside the cap.
			foreach (KeyValuePair<Component, ZoneVoice> kv in _voices) {
				if (kv.Key == null || !_keep.Contains(kv.Key)) _gone.Add(kv.Key);
			}
			for (int i = 0; i < _gone.Count; i++) {
				AudioEngine.Stop(_voices[_gone[i]].Voice);
				_voices.Remove(_gone[i]);
			}
			_gone.Clear();
		}

		/// <summary>A trap just became active (its Start coroutine was created); rescan on
		/// the next pump. Called from the Harmony postfix; records only, per the hook rule.</summary>
		internal static void MarkTrapsChanged() {
			s_trapsChanged = true;
		}

		/// <summary>
		/// Find the active traps and the components read live for each: every damage trigger
		/// (its apply flag is the armed state) and that trigger's collider (the footprint).
		/// One entry per trigger, because a trap can own several, each cycled by its own
		/// phase. Per the decompiled source the applicants sit on the trap's own object;
		/// InChildren also covers them authored a level down (inactive children included:
		/// the per-frame activeInHierarchy check decides audibility). A trap this can't
		/// resolve is logged, not skipped silently: it would be an invisible hazard. A trap
		/// with no trigger at all is likely a manual-applicant scripted hit (non-positional,
		/// nothing a place-voice could say); the warn line is the recon for whether such
		/// content exists.
		/// </summary>
		private void ScanTraps() {
			_traps.Clear();
			Trap[] traps = UnityEngine.Object.FindObjectsOfType<Trap>();
			for (int i = 0; i < traps.Length; i++) {
				Trap trap = traps[i];
				CombatApplicantTrigger[] applicants = trap.GetComponentsInChildren<CombatApplicantTrigger>(true);
				if (applicants.Length == 0) {
					Log.Warn("trap '" + trap.name + "' has no damage trigger; it will not be voiced");
					continue;
				}

				bool primed = false;
				TrapPhase[] phases = (TrapPhase[])TrapPhases.GetValue(trap);
				for (int p = 0; p < phases.Length; p++)
					if (phases[p] is TrapPhaseWaitForTrigger) primed = true;

				for (int a = 0; a < applicants.Length; a++) {
					Collider collider = applicants[a].GetComponent<Collider>();
					if (collider == null) {
						Log.Warn("trap '" + trap.name + "' trigger '" + applicants[a].name + "' has no collider; it will not be voiced");
						continue;
					}
					_traps.Add(new TrapEntry { Applicant = applicants[a], Collider = collider, Primed = primed });
				}
			}
			if (traps.Length > 0)
				Log.Info("traps: " + traps.Length + " active, " + _traps.Count + " trigger(s) voiced");
		}

		private void StopAll() {
			_lastAreas = -1;
			_lastAudible = -1;
			_traps.Clear();
			_trapLevel = null;
			if (_voices.Count == 0) return;
			foreach (KeyValuePair<Component, ZoneVoice> kv in _voices)
				AudioEngine.Stop(kv.Value.Voice);
			_voices.Clear();
		}
	}
}
