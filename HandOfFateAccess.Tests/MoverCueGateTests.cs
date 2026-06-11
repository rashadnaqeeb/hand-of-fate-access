using HandOfFateAccess.Combat;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class MoverCueGateTests {
		private readonly MoverCueGate _gate = new MoverCueGate();

		[Fact]
		public void FirstLaunchFromUnseenSource_Cues() {
			Assert.True(_gate.ShouldCueLaunch(1, 10f));
		}

		[Fact]
		public void LaunchAfterAttackCueFromSameSource_IsSuppressedWithinWindow() {
			// The Hermit's throw cue, then its bomb engaging moments later: one attack, one cue.
			_gate.NoteAttackCue(1, 10f);
			Assert.False(_gate.ShouldCueLaunch(1, 10.1f));
			Assert.False(_gate.ShouldCueLaunch(1, 10f + MoverCueGate.WindowSeconds - 0.1f));
		}

		[Fact]
		public void LaunchFromDifferentSource_CuesDespiteRecentAttackCue() {
			_gate.NoteAttackCue(1, 10f);
			Assert.True(_gate.ShouldCueLaunch(2, 10.1f));
		}

		[Fact]
		public void LaunchAfterWindowElapsed_Cues() {
			_gate.NoteAttackCue(1, 10f);
			Assert.True(_gate.ShouldCueLaunch(1, 10f + MoverCueGate.WindowSeconds));
		}

		[Fact]
		public void BarrageFromOneSource_CuesOncePerWindow() {
			Assert.True(_gate.ShouldCueLaunch(1, 10f));
			Assert.False(_gate.ShouldCueLaunch(1, 11f));
			Assert.False(_gate.ShouldCueLaunch(1, 12.9f));
			// A suppressed launch does not refresh the window: it runs from the cue that
			// played at 10, so the barrage cues again once that ages out.
			Assert.True(_gate.ShouldCueLaunch(1, 10f + MoverCueGate.WindowSeconds));
		}

		[Fact]
		public void TrapVolley_FoldsToOneCrack_ButTheNextVolleyCues() {
			// Three spears of one volley land in the same instant: one crack. The wall's
			// next volley, a few seconds later, cues again - the volley window is shorter
			// than the mover window precisely so repeating trap cycles stay audible.
			Assert.True(_gate.ShouldCueLaunch(1, 10f, MoverCueGate.VolleyWindowSeconds));
			Assert.False(_gate.ShouldCueLaunch(1, 10.05f, MoverCueGate.VolleyWindowSeconds));
			Assert.False(_gate.ShouldCueLaunch(1, 10.1f, MoverCueGate.VolleyWindowSeconds));
			Assert.True(_gate.ShouldCueLaunch(1, 10f + MoverCueGate.VolleyWindowSeconds + 0.1f, MoverCueGate.VolleyWindowSeconds));
		}

		[Fact]
		public void VolleyWindow_IsShorterThanAnAttackCycle() {
			Assert.True(MoverCueGate.VolleyWindowSeconds < MoverCueGate.WindowSeconds);
		}

		[Fact]
		public void UnknownSource_AlwaysCues_AndNeverSuppresses() {
			_gate.NoteAttackCue(MoverCueGate.UnknownSource, 10f);
			Assert.True(_gate.ShouldCueLaunch(MoverCueGate.UnknownSource, 10.1f));
			Assert.True(_gate.ShouldCueLaunch(MoverCueGate.UnknownSource, 10.2f));
		}

		[Fact]
		public void Clear_ForgetsSources() {
			_gate.NoteAttackCue(1, 10f);
			_gate.Clear();
			Assert.True(_gate.ShouldCueLaunch(1, 10.5f));
		}
	}
}
