namespace HandOfFateAccess.Input {
	/// <summary>
	/// One mod input binding: a trigger (a key or controller button, a directional, a
	/// chord) paired with what it does. The router polls each registered binding once
	/// per frame; the binding reads the live device state and acts when triggered.
	///
	/// Bindings own their own activation gate (e.g. "only on the map screen"), so the
	/// router stays a dumb pump. The status key is always active; the map cursor's
	/// bindings (Phase 5) gate themselves to the map screen via the screen stack.
	/// </summary>
	internal interface IInputBinding {
		/// <summary>Short name for logging which binding failed; not spoken.</summary>
		string Name { get; }

		/// <summary>
		/// Read the live device state and act if triggered and active. Called once per
		/// frame from the router. Reads only; never consumes the game's own input (that
		/// is done separately in the OnKey patch where an arrow must be swallowed).
		/// </summary>
		void Poll();
	}
}
