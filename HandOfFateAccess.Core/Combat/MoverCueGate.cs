using System.Collections.Generic;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Decides whether a mover hazard's launch (a lob leaving the thrower's hand, a lightning
	/// head released from a cast) gets its own dodge cue, or stays silent because the attack
	/// that spawned it already telegraphed one. Some spawning attacks cue through their own
	/// hooks (the Hermit's bomb at OnThrow, the bespoke boss actions at Begin, ranged attacks
	/// at the effect-start chokepoint); for those, a second crack at the proxy's engage would
	/// read as a second attack. Others spawn movers with no telegraph at all, and those need
	/// the launch cue. The two cannot be told apart statically, so the rule is temporal and
	/// per attacker: one cue per source per window.
	///
	/// Suppression is keyed by the attacker, never global: an unrelated enemy's cue must not
	/// swallow a lob launched at the same moment from across the arena.
	///
	/// This is a "what the player hears" decision, so it lives in Core and is unit-tested;
	/// the plugin feeds it opaque source keys and the current time.
	/// </summary>
	public sealed class MoverCueGate {
		/// <summary>Seconds after a source's cue during which that source's mover launches
		/// stay silent. Sized to cover the gap between a boss action's Begin cue and the
		/// proxy actually spawning at the end of its cast animation, while staying shorter
		/// than an enemy's attack cycle so distinct attacks still cue. Tunable live.</summary>
		public const float WindowSeconds = 3f;

		/// <summary>The shorter window for trap-fired shots: long enough to fold one
		/// volley's simultaneous spears into a single crack, short enough that the next
		/// volley (trap cycles repeat every few seconds) cues again.</summary>
		public const float VolleyWindowSeconds = 1f;

		/// <summary>Source key meaning "attacker unknown": never noted, never suppressed.</summary>
		public const int UnknownSource = 0;

		// Last cue time per source key. Mod-side event bookkeeping (when WE cued), not a
		// cache of game state. Cleared by the pump whenever combat is not live.
		private readonly Dictionary<int, float> _lastCue = new Dictionary<int, float>();

		/// <summary>Record that an attack telegraph cue played for <paramref name="sourceKey"/>
		/// at <paramref name="now"/>, so that source's mover launches go silent for the window.</summary>
		public void NoteAttackCue(int sourceKey, float now) {
			if (sourceKey == UnknownSource) return;
			_lastCue[sourceKey] = now;
		}

		/// <summary>
		/// Whether a mover launched by <paramref name="sourceKey"/> at <paramref name="now"/>
		/// should cue, with <paramref name="windowSeconds"/> as that record's suppression
		/// window (<see cref="WindowSeconds"/> for movers, <see cref="VolleyWindowSeconds"/>
		/// for trap shots). True notes the cue as played, so a sustained barrage from one
		/// source cues at most once per window (a periodic reminder, not a crack per bomb).
		/// A suppressed launch does not refresh the window: the next launch after the
		/// original cue ages out cues normally.
		/// </summary>
		public bool ShouldCueLaunch(int sourceKey, float now, float windowSeconds = WindowSeconds) {
			if (sourceKey == UnknownSource) return true;
			float last;
			if (_lastCue.TryGetValue(sourceKey, out last) && now - last < windowSeconds) return false;
			_lastCue[sourceKey] = now;
			return true;
		}

		/// <summary>Forget every source, called between fights so stale entries (or a reused
		/// instance id) never suppress a fresh fight's first cue.</summary>
		public void Clear() {
			_lastCue.Clear();
		}
	}
}
