using System;

namespace HandOfFateAccess.Audio {
	/// <summary>The audio-thread targets one placement resolves to: the two channel
	/// gains and the interaural delay. Derived once per placement change on the main
	/// thread by <see cref="Spatializer.Cue"/>; the voice ramps its live values toward
	/// these so a moving source never clicks.</summary>
	public struct SpatialCue {
		/// <summary>Equal-power channel gains, volume folded in.</summary>
		public float GainLeft;
		public float GainRight;

		/// <summary>Interaural time difference in output samples; magnitude is the far
		/// ear's lag, sign is the source side (+right/east delays the LEFT ear).</summary>
		public float ItdSamples;
	}

	/// <summary>
	/// The one placement law every positioned sound obeys. Three independent
	/// perceptual channels, all users always on headphones:
	///
	///  - east/west: an equal-power level pan plus an interaural time difference (the
	///    far ear hears the sound up to <see cref="MaxItdSeconds"/> later). Below
	///    about 1.5 kHz the time difference is the ear's dominant left/right cue and
	///    is resolved far finer than one sample, so it sharpens and externalizes
	///    placement well beyond what the level pan gives alone. Both ride the same
	///    lateral fraction, so the two cues can never disagree.
	///  - fore/aft: pitch. Stereo cannot pan front/back, so the fore/aft fraction maps
	///    to a down-biased playback rate (<see cref="PitchFor"/>): the authored sound
	///    dead ahead, darkening to <see cref="DownOctaves"/> lower dead behind, so
	///    darker means behind and bright means ahead. Down only, never up: down-shifting
	///    compresses a sample's transients cleanly while up-shifting smears them into a
	///    metallic buzz.
	///  - distance: volume, left to each feature's own falloff curve.
	///
	/// <see cref="Bearing"/> maps a ground offset onto the pan/depth fractions with a
	/// near-field crossover: beyond <see cref="PanWidth"/> they are the pure bearing
	/// (a due-west source is hard left at any distance), inside it they track the
	/// absolute offset instead, so a source passing within arm's reach glides across
	/// the field rather than snapping ear to ear, and a source on top of the player
	/// sits centered at mid pitch: it is everywhere, any direction away works.
	/// </summary>
	public static class Spatializer {
		/// <summary>Ground distance, in world units, of the near-field pan crossover:
		/// <see cref="HandOfFateAccess.Combat.RangingCurve.FullRange"/> (the player's
		/// melee reach), so "close enough to soften the bearing" and "close enough to
		/// swing at" are the same distance and retune together.</summary>
		public const float PanWidth = HandOfFateAccess.Combat.RangingCurve.FullRange;

		/// <summary>The largest interaural delay: head width over the speed of sound,
		/// about 0.22 m / 343 m/s. About 29 samples at 44.1 kHz.</summary>
		public const float MaxItdSeconds = 0.00066f;

		/// <summary>Octaves the pitch is dropped dead behind (dead ahead is the
		/// authored sound; the side line sits halfway down). 0.8 octave ~ 0.57x.
		/// Tuned by ear.</summary>
		public const float DownOctaves = 0.8f;

		/// <summary>
		/// The pan/depth fractions for a source <paramref name="right"/> world units
		/// to the camera's right (negative is left) and <paramref name="forward"/>
		/// toward screen-north (negative is south) of the listener. Pure bearing past
		/// <see cref="PanWidth"/>, absolute-offset inside it (the near-field
		/// crossover), zero on top of the listener.
		/// </summary>
		public static void Bearing(float right, float forward, out float pan, out float depth) {
			float dist = (float)Math.Sqrt(right * right + forward * forward);
			if (dist < 1e-6f) {
				pan = 0f;
				depth = 0f;
				return;
			}
			float denom = dist > PanWidth ? dist : PanWidth;
			pan = right / denom;
			depth = forward / denom;
		}

		/// <summary>
		/// The down-biased pitch ratio for a fore/aft <paramref name="depth"/> in
		/// [-1, +1] (behind to ahead): 1 (the authored sound) dead ahead, falling to
		/// <see cref="DownOctaves"/> lower dead behind, and never above 1. The side
		/// line (depth 0) sits halfway down.
		/// </summary>
		public static float PitchFor(float depth) {
			float octaves = (depth - 1f) * 0.5f * DownOctaves;  // 0 ahead, -DownOctaves behind
			return (float)Math.Pow(2.0, octaves);
		}

		/// <summary>The placement for a source at a ground offset with a caller-decided
		/// <paramref name="volume"/> (every family keeps its own falloff curve): the
		/// bearing above, its fore/aft fraction folded into the pitch. The one
		/// composition every bearing-positioned sound uses, so the placement law
		/// changes in one place. Flat sounds (no fore/aft axis) skip this and pass
		/// pitch 1 themselves.</summary>
		public static SoundParams Place(float right, float forward, float volume) {
			Bearing(right, forward, out float pan, out float depth);
			return new SoundParams(pan, PitchFor(depth), volume);
		}

		/// <summary>The audio-thread targets for a placement, at the mixer's
		/// <paramref name="sampleRate"/>. Main-thread math; the voice ramps toward
		/// the result. Pitch is not part of the cue: the voice applies it directly
		/// as its read step.</summary>
		public static SpatialCue Cue(SoundParams p, int sampleRate) {
			p = p.Clamped();

			// Equal-power pan: cos/sin over the quarter turn, volume folded in.
			float angle = (p.Pan + 1f) * 0.25f * (float)Math.PI;
			return new SpatialCue {
				GainLeft = (float)Math.Cos(angle) * p.Volume,
				GainRight = (float)Math.Sin(angle) * p.Volume,
				ItdSamples = MaxItdSeconds * sampleRate * p.Pan,
			};
		}
	}
}
