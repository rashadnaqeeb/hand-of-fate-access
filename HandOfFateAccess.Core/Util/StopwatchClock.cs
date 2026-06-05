using System.Diagnostics;

namespace HandOfFateAccess.Util {
	/// <summary>
	/// Default IClock backed by a wall-clock Stopwatch. Frame-independent and
	/// engine-free, which is all the dedup window needs -- no reason to tie it to
	/// Unity's frame time.
	/// </summary>
	public sealed class StopwatchClock : IClock {
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		public float Seconds => (float)_stopwatch.Elapsed.TotalSeconds;
	}
}
