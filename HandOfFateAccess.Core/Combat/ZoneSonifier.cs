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
	/// not class: a primed trap pulses slow and hard, an arming zone throbs softly, an active
	/// one buzzes, and inside is an unmistakable rattle at full volume regardless of phase,
	/// because the verb is the same either way - get out, and during arming getting out is
	/// free.
	///
	/// Bearing uses the same grammar as the rest of the combat audio (pan = east/west, the
	/// down-biased pitch = north/south, via <see cref="ProjectileSonifier.PitchFor"/>) so the
	/// player reads one spatial language. Volume is the proximity warning: full against the
	/// zone's edge, fading to silence at <see cref="FalloffRange"/> - unlike a projectile, a
	/// distant zone is not a live threat and silence IS the all-clear, which is also what
	/// keeps a hazard-littered arena from droning.
	///
	/// Cones (angle-limited areas) are deliberately voiced as full discs: over-warning is the
	/// safe direction for a player who cannot see the arc's facing.
	/// </summary>
	public static class ZoneSonifier {
		/// <summary>Gap to the zone's nearest dangerous point, in world units, beyond which
		/// it is not voiced at all. Shorter than the projectile falloff: a zone only matters
		/// when it constrains where you can step.</summary>
		public const float FalloffRange = 10f;

		/// <summary>Loudness with the player right against the zone's edge; inside overrides
		/// to full volume.</summary>
		public const float MaxVolume = 0.9f;

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

			float volume = inside ? 1f : MaxVolume * (1f - gap / FalloffRange);
			string clip = inside ? ZoneSynth.InsideKey
				: phase == ZonePhase.Arming ? ZoneSynth.ArmingKey
				: phase == ZonePhase.Primed ? ZoneSynth.PrimedKey : ZoneSynth.ActiveKey;

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
