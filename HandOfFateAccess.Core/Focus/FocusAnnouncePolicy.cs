namespace HandOfFateAccess.Focus {
	/// <summary>How a focus announcement is delivered relative to current speech.</summary>
	public enum SpeechMode {
		Interrupt,
		Queued,
	}

	/// <summary>
	/// Decides whether a focus announcement interrupts current speech or queues behind
	/// it, from two facts: whether the user drove the focus change directly with a
	/// keypress (navigation, cancel, or confirm) versus the game landing focus on its
	/// own, and whether a screen or overlay announced itself this frame.
	///
	/// A freshly announced context always leads: when a dialogue or screen just spoke,
	/// the focus that follows queues so it reads after that text rather than cutting it
	/// off, even when the same keypress that opened the context also placed the focus
	/// (pressing confirm to open a dialogue: the dialogue text leads, the confirm
	/// button queues behind it). With no fresh context, a user-driven focus change
	/// interrupts for responsive navigation, and a game-driven one queues so it never
	/// supersedes what the user is already hearing.
	/// </summary>
	public static class FocusAnnouncePolicy {
		public static SpeechMode Decide(bool userInitiated, bool screenJustChanged) {
			if (screenJustChanged) return SpeechMode.Queued;
			return userInitiated ? SpeechMode.Interrupt : SpeechMode.Queued;
		}
	}
}
