namespace HandOfFateAccess.Util {
	/// <summary>
	/// Monotonic time source for time-window logic (e.g. speech dedup). Abstracted
	/// so the core stays free of UnityEngine.Time and tests can supply fake time.
	/// </summary>
	public interface IClock {
		/// <summary>Monotonically increasing seconds since an arbitrary start.</summary>
		float Seconds { get; }
	}
}
