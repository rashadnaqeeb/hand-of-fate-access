namespace HandOfFateAccess.Focus {
	/// <summary>
	/// Decides whether and how to re-announce a control whose value changed in place
	/// while it still holds focus, with no selection change to drive it: a selector or
	/// toggle that rewrites its label on left/right, or a stat card whose number moves
	/// on a game event. Reuses <see cref="SpeechMode"/> for the delivery.
	///
	/// Two kinds of in-place change are treated differently. A selector/toggle changes
	/// only from the user's own input, so it is polled only just after input (so an
	/// unrelated label that animates on its own is not announced as if the user changed
	/// it) and interrupts, as the direct response to that input. A stat card (gold,
	/// food, health) changes from game events with no input of its own, so it is polled
	/// every frame and queues, so an automatic resource change while the card holds
	/// focus is still heard without cutting off whatever is speaking.
	/// </summary>
	public static class ValuePollPolicy {
		/// <summary>
		/// Whether to read the control this frame at all. A stat card is always polled;
		/// any other control only just after the user's own input. Gating here, before
		/// the readout is taken, also keeps a self-animating label from quietly advancing
		/// the stored readout, which would suppress the real change when input arrives.
		/// </summary>
		public static bool ShouldPoll(bool isStat, bool wasRecentInput) {
			return isStat || wasRecentInput;
		}

		/// <summary>
		/// How a changed readout is delivered. An automatic stat change queues so it does
		/// not cut off current speech; everything else is a direct response to the user's
		/// input and interrupts. Only meaningful once <see cref="ShouldPoll"/> is true.
		/// </summary>
		public static SpeechMode Delivery(bool isStat, bool wasRecentInput) {
			return isStat && !wasRecentInput ? SpeechMode.Queued : SpeechMode.Interrupt;
		}
	}
}
