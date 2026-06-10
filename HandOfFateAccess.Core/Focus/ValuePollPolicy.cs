namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Decides whether and how to re-announce a control whose value changed in place
	/// while it still holds focus, with no selection change to drive it: a selector or
	/// toggle that rewrites its label on left/right, or a stat card whose number moves
	/// on a game event. Reuses <see cref="SpeechMode"/> for the delivery.
	///
	/// Two kinds of watched control are treated differently. A control whose value can
	/// change without fresh input is polled every frame: a stat card (gold, food,
	/// health) moves on game events, and a settings row's applied value can land frames
	/// after the keypress (a fullscreen or resolution switch takes effect when the
	/// engine gets to it; a rebound key's label updates in a game coroutine after the
	/// scan). Everything else is polled only just after input, so an unrelated label
	/// that animates on its own is not announced as if the user changed it.
	/// </summary>
	public static class ValuePollPolicy {
		/// <summary>
		/// Whether to read the control this frame at all. An always-polled control
		/// (stat card, settings row) is read every frame; any other control only just
		/// after the user's own input. Gating here, before the readout is taken, also
		/// keeps a self-animating label from quietly advancing the stored readout,
		/// which would suppress the real change when input arrives.
		/// </summary>
		public static bool ShouldPoll(bool alwaysPoll, bool wasRecentInput) {
			return alwaysPoll || wasRecentInput;
		}

		/// <summary>
		/// How a changed readout is delivered. A change landing just after the user's
		/// own input is its direct response and interrupts; a change arriving on its
		/// own (a stat moved by a game event, a display switch completing late) queues
		/// so it does not cut off current speech. Only meaningful once
		/// <see cref="ShouldPoll"/> is true.
		/// </summary>
		public static SpeechMode Delivery(bool alwaysPoll, bool wasRecentInput) {
			return alwaysPoll && !wasRecentInput ? SpeechMode.Queued : SpeechMode.Interrupt;
		}
	}
}
