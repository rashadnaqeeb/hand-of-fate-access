using System;
using HandOfFateAccess.Audio;

namespace HandOfFateAccess.Combat {
	/// <summary>The damage phase of a zone hazard: arming (damage is off for now; leaving is
	/// free), active (standing in it hurts), or primed (a trap idling on its proximity
	/// trigger: damage is off, but approaching fires it).</summary>
	public enum ZonePhase {
		Arming,
		Active,
		Primed,
	}

	/// <summary>What the player hears for one zone hazard this frame: which loop plays, where
	/// it sits in the field, and the gap to its nearest dangerous point (the sort key when
	/// more zones are live than voices). Audible false means the zone is too far to voice.</summary>
	public struct ZoneCue {
		public bool Audible;
		public string ClipKey;
		public SoundParams Params;
		public float Distance;
		public bool Inside;
	}

	/// <summary>
	/// Turns a zone hazard's shape and phase into the loop the player hears for it. A zone is
	/// a PLACE, so unlike a projectile its voice carries one verb only: move away from the
	/// sound. The sound sits at the zone's nearest dangerous point, which makes that verb
	/// auto-correct for every shape: away from a disc's edge is radially out, away from the
	/// ring band around a safe hole is into the hole, and when the player is INSIDE, the
	/// sound sits toward the zone's center so fleeing it is the exit. State carries urgency,
	/// not class: a primed trap pulses slow and hard, an arming zone throbs softly, an
	/// active one buzzes. Inside is always full volume, but the unmistakable rattle plays
	/// only while the hazard is LIVE; underfoot on a safe beat the hazard keeps its own
	/// loop, so a trap that must be walked across (a spike floor spanning the corridor)
	/// stays timeable from inside: cross on the throb, and the flip to the rattle IS the
	/// arming edge. (The first build rattled for every inside phase, and a player crossing
	/// a floor trap heard only "you are on it" with no way to hear the cycle.)
	///
	/// Bearing uses the same grammar as the rest of the combat audio (pan = east/west, the
	/// down-biased pitch = north/south, via <see cref="ProjectileSonifier.PitchFor"/>) so the
	/// player reads one spatial language. Volume is the proximity warning on the shared
	/// <see cref="RangingCurve"/>: full with the edge within melee reach, fading to silence
	/// at <see cref="FalloffRange"/> - unlike a projectile, a distant zone is not a live
	/// threat and silence IS the all-clear, which together with the voice cap keeps a
	/// hazard-littered arena from droning.
	///
	/// Cones (angle-limited areas) are deliberately voiced as full discs: over-warning is the
	/// safe direction for a player who cannot see the arc's facing.
	/// </summary>
	public static class ZoneSonifier {
		/// <summary>Gap to the zone's nearest dangerous point, in world units, beyond which
		/// it is not voiced at all: the shared ranging curve's far end, where the volume
		/// reaches zero, so a zone fades smoothly out of existence and loudness means the
		/// same distance as every other ranged sound.</summary>
		public const float FalloffRange = RangingCurve.FloorRange;

		/// <summary>Loudness with the player right against the zone's edge; inside overrides
		/// to full volume.</summary>
		public const float MaxVolume = 0.9f;

		/// <summary>A voiced hazard keeps its voice until a contender is this fraction
		/// nearer. Without the margin, the few voices flick identity between
		/// similar-distance hazards with every step, and no loop can be attributed to a
		/// place. An inside hazard (distance zero) still seizes a voice instantly.</summary>
		public const float HoldMargin = 0.2f;

		/// <summary>Sort rank for handing the limited voices to the nearest hazards: plain
		/// distance, discounted by <see cref="HoldMargin"/> for a hazard that already holds
		/// a voice, so a handoff needs a real margin instead of a tie-break.</summary>
		public static float SelectionRank(float distance, bool held) =>
			held ? distance * (1f - HoldMargin) : distance;

		/// <summary>
		/// The cue for a zone whose center sits <paramref name="right"/> world units to the
		/// camera's right and <paramref name="forward"/> toward screen-north of the player,
		/// with the given danger radii (<paramref name="innerRadius"/> &gt; 0 makes it a ring
		/// with a safe hole) and damage <paramref name="phase"/>.
		/// </summary>
		public static ZoneCue Compose(float right, float forward, float outerRadius, float innerRadius, ZonePhase phase) {
			float dist = (float)Math.Sqrt(right * right + forward * forward);
			bool inside = dist <= outerRadius && dist >= innerRadius;

			// Gap to the nearest dangerous point: zero inside, radial outside the edge,
			// inward across the safe hole of a ring.
			float gap = inside ? 0f : (dist > outerRadius ? dist - outerRadius : innerRadius - dist);

			// Bearing of the danger. Outside (and inside, toward the exit) it points along
			// the center offset; from within a ring's safe hole the nearest band is the
			// other way, so the direction flips and fleeing the sound leads deeper into the
			// hole, which is the safe move.
			float bx = right, bf = forward;
			if (dist < innerRadius) {
				bx = -bx;
				bf = -bf;
			}

			return BuildCue(bx, bf, gap, inside, phase);
		}

		/// <summary>
		/// The cue for a hazard known only by its nearest dangerous point (a trap's collider
		/// rather than authored radii): <paramref name="right"/>/<paramref name="forward"/>
		/// are the offset to that point, or toward the hazard's center when the player is
		/// <paramref name="inside"/> its footprint, so fleeing the sound is still the exit.
		/// </summary>
		public static ZoneCue ComposePoint(float right, float forward, bool inside, ZonePhase phase) {
			float gap = inside ? 0f : (float)Math.Sqrt(right * right + forward * forward);
			return BuildCue(right, forward, gap, inside, phase);
		}

		/// <summary>
		/// The cue for a beam: a damaging line from endpoint A to endpoint B (both as
		/// player-relative right/forward offsets) with the beam collider's half-width
		/// <paramref name="radius"/>. The nearest dangerous point is the closest point on
		/// the segment, and the voice sits there both outside and inside: within the beam's
		/// width the bearing points at the beam's axis, so fleeing the sound is the
		/// perpendicular step off the line, the shortest exit. Standing exactly on the axis
		/// gives no bearing, which voices centered at mid pitch like a zone's center -
		/// either side out works. A degenerate segment (A = B) composes as a point.
		/// </summary>
		public static ZoneCue ComposeSegment(
			float rightA, float forwardA, float rightB, float forwardB, float radius, ZonePhase phase) {
			float dx = rightB - rightA;
			float df = forwardB - forwardA;
			float lengthSq = dx * dx + df * df;

			// The player sits at the origin, so the nearest point on the segment is A plus
			// the origin's clamped projection onto A-to-B.
			float t = 0f;
			if (lengthSq > 1e-9f) {
				t = -(rightA * dx + forwardA * df) / lengthSq;
				if (t < 0f) t = 0f;
				else if (t > 1f) t = 1f;
			}
			float nearRight = rightA + t * dx;
			float nearForward = forwardA + t * df;

			// From here the beam IS a disc of its half-width centered on the nearest
			// point, so the edge, inside, and bearing semantics stay single-sourced.
			return Compose(nearRight, nearForward, radius, 0f, phase);
		}

		private static ZoneCue BuildCue(float bx, float bf, float gap, bool inside, ZonePhase phase) {
			if (gap >= FalloffRange) return default(ZoneCue);

			// L1-normalize so pan and pitch share one unit of deflection by angle, the shared
			// grammar. On top of the zone's center (no bearing) the voice sits centered at
			// mid pitch: it is everywhere, any direction out works.
			float pan = 0f;
			float deflection = 0f;
			float budget = Math.Abs(bx) + Math.Abs(bf);
			if (budget > 1e-6f) {
				pan = bx / budget;
				deflection = bf / budget;
			}

			// The shared ranging curve: full within melee reach of the edge, fading to
			// silence at the curve's far end.
			float volume = inside ? 1f : MaxVolume * RangingCurve.Closeness(gap);
			// Inside is full volume always, but only a LIVE hazard rattles: a safe-beat or
			// still-arming hazard underfoot keeps its own loop, so the cycle stays audible
			// mid-crossing and the flip to the rattle is the moment it goes hot.
			string clip = phase == ZonePhase.Arming ? ZoneSynth.ArmingKey
				: phase == ZonePhase.Primed ? ZoneSynth.PrimedKey
				: inside ? ZoneSynth.InsideKey : ZoneSynth.ActiveKey;

			return new ZoneCue {
				Audible = true,
				ClipKey = clip,
				Params = new SoundParams(pan, ProjectileSonifier.PitchFor(deflection), volume),
				Distance = gap,
				Inside = inside,
			};
		}
	}
}
