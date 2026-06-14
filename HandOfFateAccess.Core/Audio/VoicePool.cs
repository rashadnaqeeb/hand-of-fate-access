using HandOfFateAccess.Util;

namespace HandOfFateAccess.Audio {
	/// <summary>
	/// Fixed-size allocator of playback voices. Engine-agnostic bookkeeping only: it
	/// tracks which of N slots are busy and stamps a generation on each so stale
	/// handles stop resolving once a slot is recycled. The backend owns the actual
	/// playing voice for each slot and maps this pool's slot index onto it.
	///
	/// This is the silent-failure surface of the audio layer: exhausting the pool
	/// means a sound the player should hear simply does not play, with nothing on
	/// screen to reveal it. <see cref="Acquire"/> logs that drop rather than failing
	/// quietly, and the pool is unit-tested off-engine. Concurrency is not a concern:
	/// all acquire and release calls come from the single Unity update pump.
	/// </summary>
	public sealed class VoicePool {
		private readonly bool[] _active;
		private readonly int[] _generation;

		public int Capacity { get; }
		public int ActiveCount { get; private set; }

		public VoicePool(int capacity) {
			Capacity = capacity;
			_active = new bool[capacity];
			_generation = new int[capacity];
		}

		/// <summary>
		/// Claims the first free slot and returns a handle to it, or
		/// <see cref="Voice.None"/> (logged) when every voice is busy.
		/// </summary>
		public Voice Acquire() {
			for (int slot = 0; slot < Capacity; slot++) {
				if (_active[slot]) continue;
				_active[slot] = true;
				ActiveCount++;
				return new Voice(slot, _generation[slot]);
			}
			Log.Warn($"voice pool exhausted ({Capacity} voices); a sound was dropped");
			return Voice.None;
		}

		/// <summary>
		/// Resolves a handle to its slot only if it is still the live owner: the slot
		/// is active and its generation still matches. False for None, an out-of-range
		/// slot, or a handle whose voice has since been released and recycled.
		/// </summary>
		public bool TryResolve(Voice voice, out int slot) {
			slot = voice.Slot;
			if (voice.Slot < 0 || voice.Slot >= Capacity) return false;
			if (!_active[voice.Slot]) return false;
			return _generation[voice.Slot] == voice.Generation;
		}

		/// <summary>Whether a slot is currently in use, by raw index. The backend
		/// sweeps these to reclaim slots whose voice has finished playing.</summary>
		public bool IsActiveSlot(int slot) => _active[slot];

		/// <summary>Frees the voice behind a handle. A stale or invalid handle is
		/// ignored, so a double release or a release after recycling is harmless.</summary>
		public void Release(Voice voice) {
			if (TryResolve(voice, out int slot))
				ReleaseSlot(slot);
		}

		/// <summary>
		/// Frees a slot by raw index and bumps its generation so any outstanding
		/// handle to it stops resolving. No-op if the slot is already free, so the
		/// backend's reclaim sweep can call it unconditionally.
		/// </summary>
		public void ReleaseSlot(int slot) {
			if (!_active[slot]) return;
			_active[slot] = false;
			_generation[slot]++;
			ActiveCount--;
		}
	}
}
