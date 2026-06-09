using HandOfFateAccess.Speech;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Gambit {
	/// <summary>
	/// Renders and caches the spoken card statuses for the chance gambit. As the Establish walk
	/// teaches which sound is which card, each slot speaks its outcome ("failure", "huge
	/// success"); SAPI gives the panned, timed voice Tolk cannot.
	///
	/// The words are the game's own localized chance-outcome titles (CHANCE_TITLE_*), so they
	/// read in the player's language with no authored text. They are rendered through
	/// <see cref="SapiRenderer"/> into mono PCM buffers, cached by language and rebuilt if the
	/// player switches language, so a stale word is never spoken. This class only renders and
	/// hands back the buffer; <see cref="GambitWatcher"/> plays it through an equal-power voice
	/// so the word pans the same way as the identity tones.
	/// </summary>
	public sealed class GambitStatusSpeech {
		private static readonly ChanceOutcome[] Outcomes = {
			ChanceOutcome.Success,
			ChanceOutcome.HugeSuccess,
			ChanceOutcome.Failure,
			ChanceOutcome.HugeFailure,
		};

		private readonly SapiRenderer _renderer = new SapiRenderer();
		private readonly float[][] _pcm = new float[4][];     // mono samples, indexed by (int)ChanceOutcome
		private readonly int[] _rate = new int[4];            // sample rate of each buffer
		private readonly float[] _durations = new float[4];   // seconds
		private bool _available;
		private bool _hasRendered;
		private string _renderedLanguage;

		public bool IsAvailable => _available;

		/// <summary>
		/// Brings up the SAPI renderer. HofSapi.dll must already be preloaded (Plugin does this
		/// beside the Tolk preload). The status words themselves are rendered lazily on first
		/// use, once the game's localization is live and the language is known.
		/// </summary>
		public bool Initialize() {
			_available = _renderer.Initialize();
			if (!_available)
				Log.Warn("gambit status speech unavailable; SAPI renderer did not initialize");
			return _available;
		}

		/// <summary>Releases the SAPI renderer (and its COM voice). Call on plugin teardown.</summary>
		public void Shutdown() {
			_renderer.Shutdown();
			_available = false;
		}

		/// <summary>Renders the words for the current language now, so a caller can read their
		/// buffers and durations before sequencing. No-op if SAPI is unavailable.</summary>
		public void Prepare() {
			if (_available) EnsureRendered();
		}

		/// <summary>The rendered mono PCM for <paramref name="outcome"/>'s word and its sample
		/// rate, for the gambit to play through its own panned voice. False if it has not
		/// rendered (SAPI unavailable, a missing key, or <see cref="Prepare"/> not yet called).</summary>
		public bool TryGetWord(ChanceOutcome outcome, out float[] pcm, out int sampleRate) {
			pcm = _pcm[(int)outcome];
			sampleRate = _rate[(int)outcome];
			return pcm != null && pcm.Length > 0;
		}

		/// <summary>The spoken length of <paramref name="outcome"/>'s word in seconds, so the
		/// gambit can sequence the slots without the words overlapping. False if it has not
		/// rendered.</summary>
		public bool TryGetDuration(ChanceOutcome outcome, out float seconds) {
			seconds = _durations[(int)outcome];
			return seconds > 0f;
		}

		// Render the four words for the current language, once per language. Re-rendering on a
		// language change replaces the cached buffers, so the spoken status always matches the
		// player's chosen language.
		private void EnsureRendered() {
			// global:: to reach the game's Localization class; the mod's own
			// HandOfFateAccess.Localization namespace otherwise shadows it here.
			string language = global::Localization.isActive ? global::Localization.instance.currentLanguage : null;
			if (_hasRendered && _renderedLanguage == language) return;

			int count = 0;
			for (int i = 0; i < _pcm.Length; i++) { _pcm[i] = null; _rate[i] = 0; _durations[i] = 0f; }
			foreach (ChanceOutcome outcome in Outcomes) {
				string key = LocalizationKey(outcome);
				string word = global::Localization.Localize(key);
				// Localize returns the key unchanged when it is missing from the loaded locale.
				// Surface that rather than render the raw key (which SAPI would spell out); the
				// status then stays silent, which is logged, not hidden.
				if (string.IsNullOrEmpty(word) || word == key) {
					Log.Warn($"chance status '{key}' has no localized text (language '{language}')");
					continue;
				}
				if (_renderer.Render(word, out float[] pcm, out int channels, out int sampleRate)) {
					_pcm[(int)outcome] = pcm;
					_rate[(int)outcome] = sampleRate;
					_durations[(int)outcome] = (float)(pcm.Length / channels) / sampleRate;
					count++;
				}
			}

			_renderedLanguage = language;
			// Only treat the language as done when every word rendered, so a transient SAPI
			// failure on one word retries on the next call instead of staying silent all session.
			_hasRendered = count == Outcomes.Length;
			if (_hasRendered)
				Log.Info($"gambit status speech rendered for language '{language}'");
			else
				Log.Warn($"gambit status speech incomplete for '{language}': {count}/{Outcomes.Length} words; will retry");
		}

		// The game's own localized chance-outcome titles. A renamed or removed key resolves to
		// itself and is caught in EnsureRendered (logged, the status stays silent), so these are
		// an audit point after a game update.
		private static string LocalizationKey(ChanceOutcome outcome) {
			switch (outcome) {
				case ChanceOutcome.Success: return "CHANCE_TITLE_SUCCESS";
				case ChanceOutcome.HugeSuccess: return "CHANCE_TITLE_HUGE_SUCCESS";
				case ChanceOutcome.Failure: return "CHANCE_TITLE_FAILURE";
				case ChanceOutcome.HugeFailure: return "CHANCE_TITLE_HUGE_FAILURE";
				default:
					// A correct caller never reaches this. Return a key that does not exist in
					// any locale so EnsureRendered's word==key check keeps it silent, rather than
					// speaking a real-but-wrong outcome.
					Log.Error($"unmapped chance outcome {(int)outcome}; leaving it silent");
					return "CHANCE_TITLE_UNMAPPED";
			}
		}
	}
}
