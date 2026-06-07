using HandOfFateAccess.Audio;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class VoicePoolTests {
		[Fact]
		public void Acquire_GivesDistinctSlots() {
			var pool = new VoicePool(3);
			var a = pool.Acquire();
			var b = pool.Acquire();
			var c = pool.Acquire();

			Assert.True(a.IsValid);
			Assert.True(b.IsValid);
			Assert.True(c.IsValid);
			Assert.Equal(3, pool.ActiveCount);
			Assert.NotEqual(a.Slot, b.Slot);
			Assert.NotEqual(b.Slot, c.Slot);
			Assert.NotEqual(a.Slot, c.Slot);
		}

		[Fact]
		public void Acquire_WhenExhausted_ReturnsNone() {
			var pool = new VoicePool(2);
			pool.Acquire();
			pool.Acquire();

			var overflow = pool.Acquire();

			Assert.False(overflow.IsValid);
			Assert.Equal(Voice.None.Slot, overflow.Slot);
			Assert.Equal(2, pool.ActiveCount);
		}

		[Fact]
		public void Release_FreesSlotForReuse() {
			var pool = new VoicePool(1);
			var first = pool.Acquire();
			pool.Release(first);

			Assert.Equal(0, pool.ActiveCount);

			var second = pool.Acquire();
			Assert.True(second.IsValid);
			Assert.Equal(first.Slot, second.Slot);
		}

		[Fact]
		public void TryResolve_LiveHandle_Succeeds() {
			var pool = new VoicePool(2);
			var v = pool.Acquire();

			Assert.True(pool.TryResolve(v, out int slot));
			Assert.Equal(v.Slot, slot);
		}

		[Fact]
		public void TryResolve_None_Fails() {
			var pool = new VoicePool(2);
			Assert.False(pool.TryResolve(Voice.None, out _));
		}

		[Fact]
		public void TryResolve_StaleHandleAfterRecycle_Fails() {
			// A handle whose slot has been released and re-acquired must not resolve,
			// so updating a finished sound cannot hijack the voice that now owns the slot.
			var pool = new VoicePool(1);
			var stale = pool.Acquire();
			pool.Release(stale);
			var fresh = pool.Acquire();

			Assert.Equal(stale.Slot, fresh.Slot);
			Assert.False(pool.TryResolve(stale, out _));
			Assert.True(pool.TryResolve(fresh, out _));
		}

		[Fact]
		public void Release_StaleHandle_IsIgnored() {
			// Double release, and release of a recycled handle, must not free the live owner.
			var pool = new VoicePool(1);
			var stale = pool.Acquire();
			pool.Release(stale);
			var fresh = pool.Acquire();

			pool.Release(stale);

			Assert.Equal(1, pool.ActiveCount);
			Assert.True(pool.TryResolve(fresh, out _));
		}

		[Fact]
		public void ReleaseSlot_ReclaimsByIndex() {
			var pool = new VoicePool(2);
			var v = pool.Acquire();

			Assert.True(pool.IsActiveSlot(v.Slot));
			pool.ReleaseSlot(v.Slot);

			Assert.False(pool.IsActiveSlot(v.Slot));
			Assert.Equal(0, pool.ActiveCount);
			Assert.False(pool.TryResolve(v, out _));
		}

		[Fact]
		public void ReleaseSlot_PartialRelease_LeavesOthersActive() {
			// The backend's reclaim sweep frees individual finished slots while others keep
			// playing; ActiveCount must track that mixed state, not drift.
			var pool = new VoicePool(3);
			var a = pool.Acquire();
			var b = pool.Acquire();
			var c = pool.Acquire();

			pool.ReleaseSlot(b.Slot);

			Assert.Equal(2, pool.ActiveCount);
			Assert.True(pool.TryResolve(a, out _));
			Assert.False(pool.TryResolve(b, out _));
			Assert.True(pool.TryResolve(c, out _));
		}

		[Fact]
		public void ReleaseSlot_AlreadyFree_IsNoOp() {
			// The backend's reclaim sweep calls this unconditionally per slot each play.
			var pool = new VoicePool(2);
			pool.ReleaseSlot(0);
			pool.ReleaseSlot(0);

			Assert.Equal(0, pool.ActiveCount);
		}
	}
}
