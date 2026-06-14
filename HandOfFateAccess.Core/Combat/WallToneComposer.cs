using System;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Turns a measured distance to a wall into the loudness and stereo position the
	/// player hears for one side. This is the "what the player hears" decision, so it
	/// lives in Core and is unit-tested off-engine; the adapter only measures distance
	/// with a raycast.
	///
	/// The four tones are authored recordings with distinct timbres: one wind for the
	/// side pair (two decorrelated takes, so left and right image separately when both
	/// sound), a brighter wind ahead, a darker one behind. Distinct timbres are
	/// deliberate, twice over: a synthesized one-wind-for-all-sides redesign (identity
	/// carried by pan and pitch alone) was field-tested and reverted, because with walls
	/// close on several sides, the common case in complex arena geometry, same-family
	/// streams blend into one unreadable mass, and decorrelation alone cannot separate
	/// more than two; per-stream timbre is what keeps a corner readable.
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
	/// Pan is bearing only, distance-independent, the same grammar every other positioned
	/// sound obeys (a projectile due west is hard left whether it is on the player or across
	/// the arena). A wall's bearing is always fully lateral: the probes are axis-locked, so
	/// the left wall sits hard left and the right wall hard right at every distance, with
	/// volume carrying distance alone. Hard pan keeps the side winds maximally clear of the
	/// centered fore/aft pair, the separation timbre would otherwise have to carry by itself;
	/// contact is already marked by the volume swelling to <see cref="MaxVolume"/> and by the
	/// one-shot collision bump, so pan need not double as an imminence cue. The fore/aft winds
	/// have no pan axis and stay centered, volume-only.
	/// </summary>
	public static class WallToneComposer {
		/// <summary>Clip keys for the four side tones, doubling as the sounds-folder file
		/// stems (walltone_right.wav ...). Indexable by side via <see cref="KeyFor"/>.</summary>
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
		/// The stereo position for a side's wind: the fore/aft winds are always centered, a
		/// side wind is hard in its ear at every distance (bearing only, the shared grammar).
		/// </summary>
		public static float PanFor(WallSide side) {
			switch (side) {
				case WallSide.Right: return 1f;
				case WallSide.Left: return -1f;
				default: return 0f;
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
