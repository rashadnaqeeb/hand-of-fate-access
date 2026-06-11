using System.Collections;
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
	/// The ground areas need no hook: the game keeps its own public registry
	/// (<c>CombatProxyArea.AllAreas</c>), polled here each frame exactly like the projectile
	/// list. The danger bound is read off the area's live collider bounds, which also tracks
	/// the grow-in of expanding zones. Holding a voice per live area object is the mod's own
	/// audio state; every shape and state input is re-read from the game each frame.
	///
	/// Beams (the mage triangle, radial bursts, the rotating boss beams) are damaging
	/// lines: not CombatProxy subclasses, no registry, spawned as children of a parent
	/// proxy, so the postfix on each beam's own Engage is the discovery point. The beam's
	/// transform plus its authored endpoint are the live truth, re-read every frame, which
	/// tracks a parent that rotates its beams for free. The full authored length is voiced
	/// from frame one (the collider grows along it, and like a growing area the footprint
	/// the beam is about to be is the warning that matters), as the arming throb until the
	/// grow completes, then active. The grow is retroactive exactly like a trap arming -
	/// anyone on the line when it fills gets hit - which the inside rattle already covers.
	/// An expiring beam (fade-out particles, trigger dead) is dropped and falls silent;
	/// expiring is terminal, and the game clears the flag again just before the deferred
	/// destroy, so waiting for the destroy instead would re-voice the corpse for a frame.
	///
	/// Segment chains (fire trails, lightning paths) keep their live segments in a private
	/// list on the parent proxy, registered by the same kind of engage postfix. One voice
	/// per chain, placed at the nearest segment's nearest point, hopping along the chain as
	/// the player moves: the chain is one hazard (a wall of fire), not dozens, and one
	/// voice cannot crowd out other zones under the voice cap. Segments damage on contact
	/// from birth, so a chain is always active; a chain whose segments all timed out simply
	/// yields no cue, and the parent's destruction prunes it.
	///
	/// Traps are scene objects with no registry, so they are discovered by a
	/// <c>FindObjectsOfType</c> scan, rerun when <c>CombatEncounter.Instance</c> changes or
	/// when <c>Trap_Start_Patch</c> flags a trap coming active (a chest or exit can switch
	/// trap hierarchies on mid-level), and then read live each frame: armed is each damage
	/// trigger's apply flag, the footprint is that trigger's collider, one voice per
	/// collider since a trap can cycle several independently. The collider need not sit on
	/// the trigger's own object - the trap rooms author it on a child (a rotating arm), or
	/// reference it from a <c>TrapPhaseCollider</c> in the cycle - so the scan tries those
	/// homes in that order. A trap's deterministic phase cycle needs no rhythm machinery of
	/// its own - the voice simply follows the armed flag, so the alternation IS the audible
	/// cycle and the player times the crossing by ear. The safe beat voices as arming
	/// (exists, damage off, leaving free), as does a trigger whose collider the cycle
	/// disabled (it cannot detect anyone, so it cannot hit, but it is not gone); a trap
	/// whose authored cycle idles on a proximity trigger instead voices its quiet time as
	/// primed, because it fires when approached and silence would read as a clear corridor.
	///
	/// The trap rooms' emitters (flame jets, spear launchers) carry no damage trigger at
	/// all: a <c>TrapPhaseCombatApplicant</c> fires a manual applicant on a timer, and the
	/// hurt arrives as a spawned proxy (a flame area, a spear projectile) that the zone or
	/// projectile path already voices while live. What was silent is the emitter's safe
	/// beat, so each origin of each phase-referenced manual applicant gets a voice of its
	/// own - arming (or primed, by the same wait-for-trigger rule), suppressed while one of
	/// the applicant's own areas is live at that origin, where the area's active voice
	/// already stands.
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
	/// A segment chain is voiced only at its nearest segment: a chain that curls around
	/// the player warns on the near side only, the voice hopping arms as the nearer one
	/// changes - whether that under-warns in practice is the validation pass's question.
	/// </summary>
	internal sealed class ZoneSonification {
		// Voices for the nearest zones only: arenas can accumulate hazards, and three
		// simultaneous loops is the most a player can usefully track on top of the wind,
		// projectiles, and cues. Silence past the falloff is itself information. Trap rooms
		// cut this to ONE: a gauntlet keeps nine-plus hazards inside the falloff at once
		// (seen in the recon log), and three loops trading places read as noise, while the
		// room is authored to be crossed one obstacle at a time. An overlapping danger
		// still seizes the voice instantly: inside is distance zero.
		private const int MaxZoneVoices = 3;
		private const int TrapRoomZoneVoices = 1;
		private const int RenderSampleRate = 44100;

		private struct ZoneVoice {
			// Fully qualified: the game's Assembly-CSharp has its own global Voice type.
			public HandOfFateAccess.Audio.Voice Voice;
			public string Clip;
		}

		private struct LiveZone {
			// The live game object the voice belongs to: an area, a beam, a chain proxy,
			// or a trap's damage trigger.
			public Component Key;
			public ZoneCue Cue;
			// The voice-selection rank: distance, discounted while this hazard already
			// holds a voice so the handoff needs a real margin (no identity flicker).
			public float Rank;
		}

		// One scanned damage trigger: the components re-read every frame. Holding these live
		// component references is the acceptable cache; armed state and shape are read off
		// them fresh. A trap can own several damage colliders (each phase-cycled
		// independently), so the scan emits one entry, and one voice, per collider, not per
		// trap or per trigger.
		private struct TrapEntry {
			public CombatApplicantTrigger Applicant;
			public Collider Collider;
			// The authored cycle idles on a proximity trigger: quiet means "fires when
			// approached", not "safe", so this trap's off-beats voice as Primed.
			public bool Primed;
		}

		// One origin of a trap's manual applicant (a flame jet nozzle, a spear launcher
		// slot): a fixed point the trap hurts FROM, voiced during the emitter's safe beat.
		// The spawned proxies themselves are voiced by the zone and projectile paths.
		private struct EmitterEntry {
			public CombatApplicantManual Applicant;
			public Transform Origin;
			public bool Primed;
		}

		// A live hostile area's resolved top container and position, collected per frame to
		// suppress emitter voices where the applicant's own area is already sounding.
		private struct AreaSource {
			public ICombatEffectContainer Container;
			public Vector3 Position;
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
		private readonly List<EmitterEntry> _emitters = new List<EmitterEntry>();
		private readonly List<AreaSource> _areaSources = new List<AreaSource>();
		private CombatEncounter _trapLevel;
		// Live beams and segment chains, recorded by their engage hooks (neither has a
		// registry) and pruned here as they end. Holding the live reference is the
		// acceptable cache: every parameter the player hears is re-read each frame.
		// Single Unity thread (the hooks fire during the game's update, this pump from
		// ours), so no synchronization, same as the mover list.
		private static readonly List<CombatProxyBeam> s_beams = new List<CombatProxyBeam>();
		private static readonly List<ChainEntry> s_chains = new List<ChainEntry>();

		// One recorded segment chain: the proxy plus its class's segment-list field,
		// resolved at record time so the pump never type-dispatches. An unknown chain
		// type warns once at record instead of throwing out of the pump every frame.
		private struct ChainEntry {
			public CombatProxy Proxy;
			public FieldInfo Segments;
		}
		// Set by Trap_Start_Patch when a trap's phase coroutine is created, i.e. the trap
		// just became active. A chest or exit can switch whole trap hierarchies on
		// mid-level, which a once-per-level scan would miss: an invisible hazard.
		private static bool s_trapsChanged;
		// Last logged counts so the diagnostic line fires only on change.
		private int _lastAreas = -1;
		private int _lastBeams = -1;
		private int _lastChains = -1;
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

		// The beam's authored line and lifecycle: m_target is the endpoint in the beam's
		// local space (its transform carries position and aim, so world geometry is
		// transform plus this); m_time/m_growTime give the grow-in window; m_expireTime
		// (a nullable, boxed null while live) marks a beam fading out, trigger already
		// dead, which must fall silent.
		private static readonly FieldInfo BeamTarget = AccessTools.Field(typeof(CombatProxyBeam), "m_target");
		private static readonly FieldInfo BeamTime = AccessTools.Field(typeof(CombatProxyBeam), "m_time");
		private static readonly FieldInfo BeamGrow = AccessTools.Field(typeof(CombatProxyBeam), "m_growTime");
		private static readonly FieldInfo BeamExpire = AccessTools.Field(typeof(CombatProxyBeam), "m_expireTime");

		// Each chain proxy's live segment list, pruned by the game as segments time out.
		private static readonly FieldInfo TrailSegments = AccessTools.Field(typeof(CombatProxyTrail), "m_segments");
		private static readonly FieldInfo LightningSegments = AccessTools.Field(typeof(CombatProxyLightning), "m_segments");

		// The trap's armed/safe switch: the one boolean its authored phase cycle flips, and
		// retroactive (arming hits everyone already standing inside). Polled per frame.
		private static readonly FieldInfo TriggerArmed = AccessTools.Field(typeof(CombatApplicantTrigger), "m_applyToTargets");
		// The trap's authored phase list, read once per scan: it spots proximity-triggered
		// traps, and its phases' serialized references are how the cycle reaches parts that
		// do not live under the applicant - the colliders a TrapPhaseCollider toggles, the
		// trigger a TrapPhaseTrigger flips, and the manual applicant a
		// TrapPhaseCombatApplicant fires.
		private static readonly FieldInfo TrapPhases = AccessTools.Field(typeof(Trap), "m_trapPhases");
		private static readonly FieldInfo PhaseColliders = AccessTools.Field(typeof(TrapPhaseCollider), "m_colliders");
		private static readonly FieldInfo PhaseTrigger = AccessTools.Field(typeof(TrapPhaseTrigger), "m_trigger");
		private static readonly FieldInfo PhaseApplicant = AccessTools.Field(typeof(TrapPhaseCombatApplicant), "m_combatApplicant");
		// A manual applicant's authored hurt-from points (the game fires its effect from the
		// origin nearest the calling phase). Empty means the applicant's own transform.
		private static readonly FieldInfo ApplicantOrigins = AccessTools.Field(typeof(CombatApplicant), "m_origins");

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

			// The Dealer's missile quick-time mutes the voices without ending the session:
			// the player is teleported and the camera overridden, so a bearing projected
			// there would mislead, but the fight is still live, so the trap scan and the
			// recon log counters stay (a full StopAll here would force a trap rescan after
			// every missile sequence). Everything re-voices when the event hands back.
			if (DealerQte.IsActive) {
				StopVoices();
				return;
			}

			// Traps live for the whole level; rescan when the encounter changes or a trap
			// becomes active (the patch flag covers traps switched on mid-level, and also
			// levels that have traps but never an encounter, if any exist). Before the area
			// loop, so the first frame's emitter suppression already knows its applicants.
			CombatEncounter encounter = CombatEncounter.Instance;
			if (encounter != _trapLevel || s_trapsChanged) {
				_trapLevel = encounter;
				s_trapsChanged = false;
				ScanTraps();
			}

			List<CombatProxyArea> areas = CombatProxyArea.AllAreas;
			_live.Clear();
			_areaSources.Clear();
			for (int i = 0; i < areas.Count; i++) {
				CombatProxyArea area = areas[i];
				if (area == null || (bool)AreaExpiring.GetValue(area)) continue;
				if (!Hostility.ThreatensPlayer(area.Effect.Source)) continue;

				// Remember whose area this is while emitters are in play: an emitter's own
				// live area at an origin replaces that origin's safe-beat voice.
				if (_emitters.Count > 0)
					_areaSources.Add(new AreaSource {
						Container = CombatUtils.GetTopContainer(area.Effect),
						Position = area.transform.position,
					});

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
				AddLive(area, cue);
			}

			// Beams: the segment is the transform's position to its authored local endpoint,
			// re-read live so a rotating parent sweeps the voice with the line. The full
			// length is voiced while the collider grows along it, as arming (the grow is
			// retroactive, but leaving during it is free - trap semantics, and standing on
			// the line already rattles).
			for (int i = s_beams.Count - 1; i >= 0; i--) {
				CombatProxyBeam beam = s_beams[i];
				// Expiring is terminal (the trigger is already dead, only the fade-out
				// remains), so the record is dropped at first sight: the game clears the
				// flag again just before the deferred destroy, and waiting for the destroy
				// would re-voice the corpse as active for that frame. Inactive covers a
				// pool-freed beam (ObjectUtils.Destroy frees pooled objects, which never go
				// Unity-null); a pooled reuse re-fires Engage and re-records.
				if (beam == null || !beam.gameObject.activeInHierarchy
						|| BeamExpire.GetValue(beam) != null) {
					s_beams.RemoveAt(i);
					continue;
				}

				Transform beamTransform = beam.transform;
				Vector3 start = beamTransform.position;
				Vector3 end = beamTransform.TransformPoint((Vector3)BeamTarget.GetValue(beam));
				ZonePhase beamPhase =
					Time.time - (float)BeamTime.GetValue(beam) < (float)BeamGrow.GetValue(beam)
					? ZonePhase.Arming : ZonePhase.Active;
				// The capsule's authored radius is the beam's danger half-width (required by
				// the class, so resolving it is not a guard). The capsule runs along local Z,
				// so its radius scales by the larger perpendicular axis.
				Vector3 beamScale = beamTransform.lossyScale;
				float radius = beam.GetComponent<CapsuleCollider>().radius
					* Mathf.Max(beamScale.x, beamScale.y);

				frame.Project(start, out float rightA, out float forwardA);
				frame.Project(end, out float rightB, out float forwardB);
				ZoneCue beamCue = ZoneSonifier.ComposeSegment(rightA, forwardA, rightB, forwardB, radius, beamPhase);
				if (!beamCue.Audible) continue;
				AddLive(beam, beamCue);
			}

			// Segment chains: one voice per chain at its nearest live segment, the same
			// collider nearest-point grammar as traps. A segment that carries no collider
			// is voiced at its position - a one-interval-long piece, so the point stands in
			// for it safely.
			for (int i = s_chains.Count - 1; i >= 0; i--) {
				CombatProxy chain = s_chains[i].Proxy;
				// Inactive covers a pool-freed proxy, which never goes Unity-null.
				if (chain == null || !chain.gameObject.activeInHierarchy) {
					s_chains.RemoveAt(i);
					continue;
				}

				IList segments = (IList)s_chains[i].Segments.GetValue(chain);
				Component nearest = null;
				Vector3 nearestPoint = default(Vector3);
				float nearestSq = float.MaxValue;
				for (int s = 0; s < segments.Count; s++) {
					Component segment = (Component)segments[s];
					if (segment == null) continue;
					Collider segCollider = segment.GetComponent<Collider>();
					Vector3 point = segCollider != null
						? segCollider.ClosestPointOnBounds(frame.Origin) : segment.transform.position;
					float distSq = (point - frame.Origin).sqrMagnitude;
					if (distSq < nearestSq) {
						nearestSq = distSq;
						nearestPoint = point;
						nearest = segment;
					}
				}
				if (nearest == null) continue;

				// Insideness is horizontal like the traps'. Inside, the bearing retargets
				// the nearest point on the chain's AXIS (each segment is spawned facing the
				// lay direction), so fleeing the sound steps perpendicular off the line, the
				// shortest exit - the beam's inside grammar. A segment's bounds center would
				// instead point ALONG the trail and walk the player down the fire.
				frame.Project(nearestPoint, out float chainRight, out float chainForward);
				bool inside = chainRight * chainRight + chainForward * chainForward < 0.01f;
				if (inside) {
					Vector3 toSegment = nearest.transform.position - frame.Origin;
					Vector3 axis = nearest.transform.forward;
					Vector3 toAxis = toSegment - axis * Vector3.Dot(toSegment, axis);
					frame.Project(frame.Origin + toAxis, out chainRight, out chainForward);
				}

				ZoneCue chainCue = ZoneSonifier.ComposePoint(chainRight, chainForward, inside, ZonePhase.Active);
				if (!chainCue.Audible) continue;
				AddLive(chain, chainCue);
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
				// Keyed by the collider: several entries can share one applicant, and each
				// collider is its own place with its own voice.
				AddLive(trap.Collider, cue);
			}

			// Emitter origins: the safe-beat marker for a trap that hurts via spawned
			// proxies. Never voiced as inside (idle, nothing is there to get out of) and
			// silent while the applicant's own area is live at the spot - the area's active
			// voice IS this emitter firing.
			for (int i = 0; i < _emitters.Count; i++) {
				EmitterEntry emitter = _emitters[i];
				if (emitter.Origin == null || !emitter.Origin.gameObject.activeInHierarchy)
					continue;
				Vector3 position = emitter.Origin.position;

				bool covered = false;
				for (int s = 0; s < _areaSources.Count; s++) {
					if (!ReferenceEquals(_areaSources[s].Container, emitter.Applicant)) continue;
					Vector3 gap = _areaSources[s].Position - position;
					gap.y = 0f;
					if (gap.sqrMagnitude < 4f) {
						covered = true;
						break;
					}
				}
				if (covered) continue;

				frame.Project(position, out float emitterRight, out float emitterForward);
				ZoneCue emitterCue = ZoneSonifier.ComposePoint(emitterRight, emitterForward, false,
					emitter.Primed ? ZonePhase.Primed : ZonePhase.Arming);
				if (!emitterCue.Audible) continue;
				AddLive(emitter.Origin, emitterCue);
			}

			// Diagnostic: how many areas the game has live versus how many hazards are near
			// enough to voice. Logged only when the counts change.
			if (areas.Count != _lastAreas || s_beams.Count != _lastBeams
					|| s_chains.Count != _lastChains || _live.Count != _lastAudible) {
				Log.Debug("zones: " + areas.Count + " areas, " + s_beams.Count + " beams, "
					+ s_chains.Count + " chains, " + _traps.Count + " traps, " + _live.Count + " audible");
				_lastAreas = areas.Count;
				_lastBeams = s_beams.Count;
				_lastChains = s_chains.Count;
				_lastAudible = _live.Count;
			}

			// Keep only the nearest voices, by held-discounted rank so a voiced hazard does
			// not lose its loop to a stranger a half-step closer; an inside zone has
			// distance zero, so it always survives the cut. Trap rooms keep ONE voice:
			// the gauntlet is crossed one obstacle at a time.
			int cap = CombatGate.IsTrapRoom ? TrapRoomZoneVoices : MaxZoneVoices;
			if (_live.Count > cap) {
				_live.Sort((a, b) => a.Rank.CompareTo(b.Rank));
				_live.RemoveRange(cap, _live.Count - cap);
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

		// Stage a hazard for this frame's voice selection. The rank discount applies while
		// the hazard held a voice LAST frame (_voices is last frame's assignment at this
		// point in the pump), which is exactly the stickiness the handoff margin wants.
		private void AddLive(Component key, ZoneCue cue) {
			_live.Add(new LiveZone {
				Key = key,
				Cue = cue,
				Rank = ZoneSonifier.SelectionRank(cue.Distance, _voices.ContainsKey(key)),
			});
		}

		/// <summary>A trap just became active (its Start coroutine was created); rescan on
		/// the next pump. Called from the Harmony postfix; records only, per the hook rule.</summary>
		internal static void MarkTrapsChanged() {
			s_trapsChanged = true;
		}

		/// <summary>Track a beam from its engage hook, which has already filtered for hostile
		/// parents; the pump voices its live line until it expires or is destroyed.</summary>
		internal static void RecordBeam(CombatProxyBeam beam) {
			if (!s_beams.Contains(beam)) s_beams.Add(beam);
		}

		/// <summary>Track a segment chain (trail or lightning) from its engage hook, which has
		/// already filtered for hostile sources; the pump voices its nearest live segment
		/// until the proxy ends. The segment-list field is bound here, where the type is
		/// known; a chain type this does not recognize is logged, not voiced wrong.</summary>
		internal static void RecordChain(CombatProxy chain) {
			for (int i = 0; i < s_chains.Count; i++)
				if (s_chains[i].Proxy == chain) return;
			FieldInfo segments =
				chain is CombatProxyTrail ? TrailSegments
				: chain is CombatProxyLightning ? LightningSegments : null;
			if (segments == null) {
				Log.Warn("chain proxy '" + chain.GetType().Name + "' has no known segment list; it will not be voiced");
				return;
			}
			s_chains.Add(new ChainEntry { Proxy = chain, Segments = segments });
		}

		/// <summary>
		/// Find the active traps and the components read live for each. Damage triggers (the
		/// apply flag is the armed state) come from the trap's hierarchy plus any a
		/// TrapPhaseTrigger reaches only by reference; each trigger's footprint colliders are
		/// looked for on its own object, then among its child trigger colliders (the trap
		/// rooms author them on the moving parts), then in the cycle's TrapPhaseCollider
		/// references. Manual applicants fired by a TrapPhaseCombatApplicant become emitter
		/// entries, one per authored origin. Inactive children are included everywhere: the
		/// per-frame activeInHierarchy check decides audibility. A trap or trigger this
		/// can't resolve is logged, not skipped silently: it would be an invisible hazard.
		/// </summary>
		private void ScanTraps() {
			_traps.Clear();
			_emitters.Clear();
			Trap[] traps = UnityEngine.Object.FindObjectsOfType<Trap>();
			for (int i = 0; i < traps.Length; i++) {
				Trap trap = traps[i];
				TrapPhase[] phases = (TrapPhase[])TrapPhases.GetValue(trap);

				bool primed = false;
				for (int p = 0; p < phases.Length; p++)
					if (phases[p] is TrapPhaseWaitForTrigger) primed = true;

				List<CombatApplicantTrigger> applicants = new List<CombatApplicantTrigger>(
					trap.GetComponentsInChildren<CombatApplicantTrigger>(true));
				List<Collider> cycleColliders = new List<Collider>();
				for (int p = 0; p < phases.Length; p++) {
					TrapPhaseTrigger triggerPhase = phases[p] as TrapPhaseTrigger;
					if (triggerPhase != null) {
						CombatApplicantTrigger referenced = (CombatApplicantTrigger)PhaseTrigger.GetValue(triggerPhase);
						if (referenced != null && !applicants.Contains(referenced))
							applicants.Add(referenced);
					}
					TrapPhaseCollider colliderPhase = phases[p] as TrapPhaseCollider;
					if (colliderPhase != null) {
						Collider[] referenced = (Collider[])PhaseColliders.GetValue(colliderPhase);
						for (int c = 0; c < referenced.Length; c++)
							if (referenced[c] != null && !cycleColliders.Contains(referenced[c]))
								cycleColliders.Add(referenced[c]);
					}
				}

				int trapsBefore = _traps.Count;
				for (int a = 0; a < applicants.Count; a++)
					AddTriggerEntries(trap, applicants[a], cycleColliders, primed);

				int emittersBefore = _emitters.Count;
				for (int p = 0; p < phases.Length; p++) {
					TrapPhaseCombatApplicant applicantPhase = phases[p] as TrapPhaseCombatApplicant;
					if (applicantPhase == null) continue;
					CombatApplicantManual applicant = (CombatApplicantManual)PhaseApplicant.GetValue(applicantPhase);
					if (TrackedEmitter(applicant)) continue;
					Transform[] origins = (Transform[])ApplicantOrigins.GetValue(applicant);
					if (origins == null || origins.Length == 0)
						origins = new Transform[] { applicant.transform };
					for (int o = 0; o < origins.Length; o++)
						if (origins[o] != null)
							_emitters.Add(new EmitterEntry { Applicant = applicant, Origin = origins[o], Primed = primed });
				}

				if (_traps.Count == trapsBefore && _emitters.Count == emittersBefore)
					Log.Warn("trap '" + trap.name + "' has no damage trigger or emitter; it will not be voiced");
			}
			if (traps.Length > 0)
				Log.Info("traps: " + traps.Length + " active, " + _traps.Count + " trigger collider(s), "
					+ _emitters.Count + " emitter origin(s) voiced");
		}

		// One entry per damage collider: on the applicant's own object in the arena layout,
		// on its children in the trap rooms (a rotating arm; trigger colliders only, and not
		// one a nested applicant owns), or failing both, the colliders the trap's cycle
		// toggles by reference. A collider already entered (two applicants sharing a cycle
		// list) keeps its first entry.
		private void AddTriggerEntries(Trap trap, CombatApplicantTrigger applicant,
				List<Collider> cycleColliders, bool primed) {
			Collider[] own = applicant.GetComponents<Collider>();
			if (own.Length > 0) {
				for (int i = 0; i < own.Length; i++) AddTrapEntry(applicant, own[i], primed);
				return;
			}

			bool added = false;
			Collider[] children = applicant.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < children.Length; i++) {
				Collider collider = children[i];
				if (!collider.isTrigger || OwnedByNestedApplicant(collider.transform, applicant)) continue;
				AddTrapEntry(applicant, collider, primed);
				added = true;
			}
			if (added) return;

			for (int i = 0; i < cycleColliders.Count; i++) {
				AddTrapEntry(applicant, cycleColliders[i], primed);
				added = true;
			}
			if (!added)
				Log.Warn("trap '" + trap.name + "' trigger '" + applicant.name + "' has no collider; it will not be voiced");
		}

		private void AddTrapEntry(CombatApplicantTrigger applicant, Collider collider, bool primed) {
			for (int i = 0; i < _traps.Count; i++)
				if (_traps[i].Collider == collider) return;
			_traps.Add(new TrapEntry { Applicant = applicant, Collider = collider, Primed = primed });
		}

		// A collider on or under another applicant's object is that applicant's volume.
		private static bool OwnedByNestedApplicant(Transform collider, CombatApplicantTrigger owner) {
			for (Transform node = collider; node != null && node != owner.transform; node = node.parent)
				if (node.GetComponent<CombatApplicant>() != null) return true;
			return false;
		}

		private bool TrackedEmitter(CombatApplicantManual applicant) {
			for (int i = 0; i < _emitters.Count; i++)
				if (_emitters[i].Applicant == applicant) return true;
			return false;
		}

		/// <summary>Release every playing voice, keeping all session state (the trap scan,
		/// the beam/chain records, the log counters): the mute half of StopAll, used alone
		/// while the fight is live but bearings are not trustworthy.</summary>
		private void StopVoices() {
			if (_voices.Count == 0) return;
			foreach (KeyValuePair<Component, ZoneVoice> kv in _voices)
				AudioEngine.Stop(kv.Value.Voice);
			_voices.Clear();
		}

		private void StopAll() {
			_lastAreas = -1;
			_lastBeams = -1;
			_lastChains = -1;
			_lastAudible = -1;
			_traps.Clear();
			_emitters.Clear();
			_trapLevel = null;
			// s_beams and s_chains are deliberately NOT cleared: the engage hooks fire once
			// per hazard, so a transient frame without a player or camera mid-fight must not
			// orphan records nothing can re-create (traps rescan and areas re-poll; these
			// cannot). Entries for ended hazards prune in the pump's next live frame.
			StopVoices();
		}
	}
}
