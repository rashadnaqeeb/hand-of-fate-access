using System;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Speech {
	/// <summary>
	/// Central speech dispatch point for ALL speech output in the mod.
	/// No code should call SpeechEngine.Say() directly -- all speech flows through here.
	///
	/// Pipeline: Caller -> SpeechPipeline -> TextFilter -> SpeechEngine -> Tolk
	/// </summary>
	public static class SpeechPipeline {
		/// <summary>
		/// When false, all speech methods return immediately without speaking.
		/// </summary>
		private static bool _enabled = true;

		private static string _lastInterruptText;
		private static float _lastInterruptTime;
		private const float DeduplicateWindowSeconds = 0.05f;

		/// <summary>
		/// Time source for the dedup window. Defaults to a wall-clock stopwatch;
		/// tests inject a fake clock.
		/// </summary>
		internal static IClock Clock = new StopwatchClock();

		/// <summary>
		/// Speech sink. Defaults to SpeechEngine.Say; tests replace this to
		/// capture output without a real backend.
		/// </summary>
		internal static Action<string, bool> SpeakAction = SpeechEngine.Say;

		/// <summary>
		/// Whether the pipeline is active. When false (mod toggled off),
		/// all methods return immediately.
		/// </summary>
		public static bool IsActive => _enabled;

		/// <summary>
		/// Enable or disable the speech pipeline.
		/// </summary>
		internal static void SetEnabled(bool enabled) {
			_enabled = enabled;
		}

		/// <summary>
		/// Reset pipeline state for test isolation.
		/// </summary>
		internal static void Reset() {
			_lastInterruptText = null;
			_lastInterruptTime = 0f;
			_enabled = true;
		}

		/// <summary>
		/// Interrupt mode: stop current speech and speak immediately.
		/// Used for navigation and announcements where responsiveness matters.
		/// </summary>
		/// <param name="text">Raw text (will be filtered through TextFilter).</param>
		public static void SpeakInterrupt(string text) {
			if (!_enabled) return;

			string filtered = TextFilter.FilterForSpeech(text);
			if (string.IsNullOrEmpty(filtered)) return;
			float now = Clock.Seconds;
			if (filtered == _lastInterruptText && now - _lastInterruptTime < DeduplicateWindowSeconds)
				return;
			_lastInterruptText = filtered;
			_lastInterruptTime = now;
			SpeakAction(filtered, true);
		}

		/// <summary>
		/// Queue mode: speak after any current speech finishes, without interrupting.
		/// Used when multiple pieces of info should be spoken in sequence.
		/// </summary>
		/// <param name="text">Raw text (will be filtered through TextFilter).</param>
		public static void SpeakQueued(string text) {
			if (!_enabled) return;

			string filtered = TextFilter.FilterForSpeech(text);
			if (string.IsNullOrEmpty(filtered)) return;
			SpeakAction(filtered, false);
		}
	}
}
