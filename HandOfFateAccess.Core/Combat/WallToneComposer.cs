using System;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Turns a measured distance to a wall into the loudness, stereo position, and pitch
	/// the player hears for one side. This is the "what the player hears" decision, so it
	/// lives in Core and is unit-tested off-engine; the adapter only measures distance
	/// with a raycast.
	///
	/// The four tones are decorrelated takes of one wind (<see cref="Audio.WindSynth"/>),
	/// told apart by where they sit, not by what they are: the side winds by pan, the
	/// fore/aft pair by the bearing grammar's pitch axis (ahead unshifted and brightest,
	/// behind <see cref="PitchSpanOctaves"/> darker, the souther-is-darker rule every
	/// positioned sound follows). One wall instrument to learn instead of three, and a
	/// corner's two winds blend into a thicker wind instead of competing timbres.
	///
	/// They run continuously through a fight rather than starting and stopping
	/// as walls cross the range edge: in cluttered arenas the nearest object on a side
	/// flickers in and out and swaps identity constantly, and restarting a loop each time
	/// clicks and lurches. Instead each side has a target volume (peaking at
	/// <see cref="MaxVolume"/> at the player's own position, falling linearly to silence at
	/// <see cref="Range"/>, and silent when nothing is in range) that the live volume eases
	/// toward with <see cref="Smooth"/>, so
	/// the field glides as the player moves instead of stuttering.
	///
	/// A side wind's pan is gated on distance: it rests at <see cref="RestPan"/> through
	/// most of the range, with volume carrying distance, and only inside
	/// <see cref="PanGateRange"/> starts sliding outward, fully in-ear at contact. The
	/// onset of stereo motion is its own cue (this wall is the one you are about to walk
	/// into), a channel a swelling volume alone does not carry. The slide is into the ear,
	/// never toward center: a wall's true bearing is always fully lateral, and drifting
	/// centerward would walk the side wind into the fore/aft pair's position. Walls can
	/// take a distance-dependent pan that no other positioned sound is allowed: the probes
	/// are axis-locked, so there is no diagonal bearing for the moving pan to be misread
	/// as. The fore/aft winds have no pan axis and stay centered, volume-only.
	/// </summary>
	public static class WallToneComposer {
		/// <summary>Clip keys for the four side winds, registered from
		/// <see cref="Audio.WindSynth"/> renders at startup (one seed per side).
		/// Indexable by side via <see cref="KeyFor"/>.</summary>
		public const string RightKey = "walltone_right";
		public const string LeftKey = "walltone_left";
		public const string AboveKey = "walltone_above";
		public const string BelowKey = "walltone_below";

		/// <summary>Clip key (and file stem) for the wall collision bump, the one-shot
		/// sibling of the continuous side tones.</summary>
		public const string CollisionKey = "walltone_collision";

		/// <summary>Distance, in world units, at and beyond which a wall is silent. The
		/// volume falls linearly to zero here, so it is the audible edge. Kept short so
		/// the tones speak to walls about to stop the player, not distant geometry.</summary>
		public const float Range = 5f;

		/// <summary>Loudness at the closest approach (the player's own position). The
		/// curve never exceeds this, so the tones sit gently under the action rather than
		/// reaching full scale.</summary>
		public const float MaxVolume = 0.5f;

		/// <summary>A side wind's stereo position at rest (unsigned; the left wind sits at
		/// minus this): strongly lateral, clearly that side, with the last stretch to hard
		/// pan held back as the contact-imminent cue.</summary>
		public const float RestPan = 0.7f;

		/// <summary>Distance, in world units, inside which a side wind's pan leaves
		/// <see cref="RestPan"/> and slides linearly to fully in-ear at contact. Half of
		/// <see cref="Range"/>: the wind is established by volume first, then starts
		/// moving.</summary>
		public const float PanGateRange = 2.5f;

		/// <summary>Octaves between the ahead wind (unshifted, brightest) and the behind
		/// wind (darkest), the bearing grammar's souther-is-darker axis. Wider than the
		/// projectile span: a wall's pitch is its identity, not a live bearing readout,
		/// so the spread is sized for instant telling-apart. Matches the spectral spread
		/// the original three authored recordings had.</summary>
		public const float PitchSpanOctaves = 1.5f;

		// Time constant, in seconds, of the volume easing: roughly the time to close most
		// of the gap to a new target. Small enough to track movement, large enough to turn
		// the per-frame jitter of a cluttered arena into a smooth swell.
		private const float SmoothingTime = 0.12f;

		/// <summary>
		/// The volume a wall on a side should reach at <paramref name="distance"/> world
		/// units: full at zero, falling linearly to zero at <see cref="Range"/>, and zero
		/// when there is no wall in range (a non-finite or negative distance, which is how
		/// the adapter reports "nothing hit").
		/// </summary>
		public static float TargetVolume(float distance) {
			// NaN fails every comparison, so the range test rejects it too.
			if (!(distance >= 0f) || distance >= Range) return 0f;
			return MaxVolume * (Range - distance) / Range;
		}

		/// <summary>The clip key for a side's tone.</summary>
		public static string KeyFor(WallSide side) {
			switch (side) {
				case WallSide.Right: return RightKey;
				case WallSide.Left: return LeftKey;
				case WallSide.Above: return AboveKey;
				default: return BelowKey;
			}
		}

		/// <summary>
		/// The stereo position for a side's wind with the wall at <paramref name="distance"/>
		/// world units: the fore/aft winds are always centered; a side wind rests at
		/// <see cref="RestPan"/> (also for a degenerate or out-of-gate distance, so a
		/// vanished wall settles back to rest) and slides linearly to fully in-ear at
		/// contact once inside <see cref="PanGateRange"/>.
		/// </summary>
		public static float PanFor(WallSide side, float distance) {
			float sign;
			switch (side) {
				case WallSide.Right: sign = 1f; break;
				case WallSide.Left: sign = -1f; break;
				default: return 0f;
			}
			// NaN fails the comparison and rests, like the volume curve's range test.
			if (!(distance >= 0f) || distance >= PanGateRange) return sign * RestPan;
			return sign * (1f - (1f - RestPan) * (distance / PanGateRange));
		}

		/// <summary>The fixed pitch for a side's wind, the playback rate its loop always
		/// plays at: ahead unshifted (the synth's authored brightness), behind the full
		/// <see cref="PitchSpanOctaves"/> down, the side winds halfway, per the shared
		/// souther-is-darker axis (left/right sit at zero north/south deflection).</summary>
		public static float PitchFor(WallSide side) {
			switch (side) {
				case WallSide.Above: return 1f;
				case WallSide.Below: return (float)Math.Pow(2.0, -PitchSpanOctaves);
				default: return (float)Math.Pow(2.0, -PitchSpanOctaves * 0.5);
			}
		}

		/// <summary>
		/// Eases <paramref name="current"/> a frame's worth toward <paramref name="target"/>,
		/// framerate-independently (the same wall-clock response at any frame rate). A
		/// non-positive <paramref name="deltaSeconds"/> leaves the value unchanged.
		/// </summary>
		public static float Smooth(float current, float target, float deltaSeconds) {
			if (deltaSeconds <= 0f) return current;
			float t = 1f - (float)Math.Exp(-deltaSeconds / SmoothingTime);
			return current + (target - current) * t;
		}
	}
}
