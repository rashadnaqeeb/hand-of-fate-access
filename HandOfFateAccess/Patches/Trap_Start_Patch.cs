using HandOfFateAccess.Combat;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Postfix on Trap.Start, an iterator method, so the postfix runs when the trap's phase
	/// coroutine is created - the moment the trap becomes active. That covers traps a chest
	/// or exit switches on mid-level (their enable-on-complete arrays), which a once-per-level
	/// scan would miss: a dangerous, unvoiced hazard. Flags the zone pump to rescan; records
	/// only, never speaks or plays.
	/// </summary>
	internal static class Trap_Start_Patch {
		private static void Postfix() => ZoneSonification.MarkTrapsChanged();
	}
}
