using System;

namespace HandOfFateAccess.Audio {
	/// <summary>The audio-thread targets one placement resolves to: the two channel
	/// gains, the interaural delay, and the rear low-pass. Derived once per placement
	/// change on the main thread by <see cref="Spatializer.Cue"/>; the voice ramps its
	/// live values toward these so a moving source never clicks.</summary>
	public struct SpatialCue {
		/// <summary>Equal-power channel gains, volume folded in.</summary>
		public float GainLeft;
		public float GainRight;

		/// <summary>Interaural time difference in output samples; magnitude is the far
		/// ear's lag, sign is the source side (+right/east delays the LEFT ear).</summary>
		public float ItdSamples;

		/// <summary>The rear (wet) path's low-pass cutoff; lower is more muffled.
		/// <see cref="Spatializer.OpenHz"/> is effectively transparent.</summary>
		public float CutoffHz;

		/// <summary>How much of the filtered copy replaces the dry signal, 0 (ahead or
		/// beside) to <see cref="Spatializer.MaxWet"/> (dead behind). The dry remainder
		/// is always kept so a bright, narrowband cue can never filter into silence:
		/// behind reads as quieter and darker, never as gone.</summary>
		public float WetMix;
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
	///  - fore/aft: timbre. Stereo cannot pan front/back, so a source behind the
	///    player is progressively low-passed, blended wet/dry (see
	///    <see cref="SpatialCue.WetMix"/>): muffled means behind, bright means ahead,
	///    the long-standing audiogame convention. Ahead and beside are bit-exact dry.
	///  - distance: volume, left to each feature's own falloff curve.
	///
	/// <see cref="Bearing"/> maps a ground offset onto the pan/depth fractions with a
	/// near-field crossover: beyond <see cref="PanWidth"/> they are the pure bearing
	/// (a due-west source is hard left at any distance), inside it they track the
	/// absolute offset instead, so a source passing within arm's reach glides across
	/// the field rather than snapping ear to ear, and a source on top of the player
	/// sits centered and dry: it is everywhere, any direction away works.
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

		/// <summary>Wet-path cutoff with no rear muffle: effectively transparent.</summary>
		public const float OpenHz = 20000f;

		/// <summary>Wet-path cutoff dead behind: heavily muffled. Tuned by ear.</summary>
		public const float MuffledHz = 500f;

		/// <summary>Wet mix dead behind: half muffled, half dry, so a bright cue loses
		/// about 6 dB behind and the dry remainder keeps every cue audible. The value
		/// wotr-access shipped and its users validated; raise it if behind reads too
		/// subtly against what the old pitch drop conveyed.</summary>
		public const float MaxWet = 0.5f;

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

		/// <summary>The placement for a source at a ground offset with a caller-decided
		/// <paramref name="volume"/> (every family keeps its own falloff curve): the
		/// bearing above, the clip at its authored pitch. The one composition every
		/// bearing-positioned sound uses, so the placement law changes in one place.</summary>
		public static SoundParams Place(float right, float forward, float volume) {
			Bearing(right, forward, out float pan, out float depth);
			return new SoundParams(pan, depth, 1f, volume);
		}

		/// <summary>The audio-thread targets for a placement, at the mixer's
		/// <paramref name="sampleRate"/>. Main-thread math; the voice ramps toward
		/// the result.</summary>
		public static SpatialCue Cue(SoundParams p, int sampleRate) {
			p = p.Clamped();

			// Equal-power pan: cos/sin over the quarter turn, volume folded in.
			float angle = (p.Pan + 1f) * 0.25f * (float)Math.PI;
			var cue = new SpatialCue {
				GainLeft = (float)Math.Cos(angle) * p.Volume,
				GainRight = (float)Math.Sin(angle) * p.Volume,
				ItdSamples = MaxItdSeconds * sampleRate * p.Pan,
				CutoffHz = OpenHz,
				WetMix = 0f,
			};

			// Only the rear hemisphere is processed, ramping from dry at the side line
			// to muffled-and-mixed dead behind. The cutoff interpolates in log space so
			// the muffle deepens evenly by ear (evenly in octaves, not hertz).
			if (p.Depth < 0f) {
				float back = -p.Depth;
				cue.CutoffHz = OpenHz * (float)Math.Pow(MuffledHz / OpenHz, back);
				cue.WetMix = back * MaxWet;
			}
			return cue;
		}
	}
}
