using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// A fixed pool of <see cref="ProjectileVoice"/> objects, each a live mono source the
	/// spatial chain hosts while it plays. The loop is synthesized, not played from a clip,
	/// so the pool needs no source PCM; it only hands each voice a distinct noise seed at
	/// construction.
	///
	/// Exhaustion is the silent-failure surface here, so <see cref="Acquire"/> logs and returns
	/// null when every voice is busy rather than dropping a threat without trace.
	/// </summary>
	internal sealed class ProjectileVoicePool {
		// A stopped engine voice fades and drains for a couple of audio blocks, during
		// which the released source must not be re-Played (two engine voices would pull
		// one synth). Released voices cool in _draining until their engine voice dies;
		// the spare objects cover that window so a busy same-frame churn (several
		// projectiles dying as several spawn) still finds free voices.
		private const int DrainSpare = 8;

		private readonly List<ProjectileVoice> _all;
		private readonly Stack<ProjectileVoice> _free;
		private readonly List<ProjectileVoice> _draining = new List<ProjectileVoice>();

		public ProjectileVoicePool(int count) {
			int total = count + DrainSpare;
			_all = new List<ProjectileVoice>(total);
			_free = new Stack<ProjectileVoice>(total);

			// Generate at the backend's mixer rate so it resamples nothing and the tumble sounds
			// exactly as it did when Unity ran the synth at the device rate.
			int rate = AudioEngine.OutputSampleRate > 0 ? AudioEngine.OutputSampleRate : 44100;

			for (int i = 0; i < total; i++) {
				// Distinct seed per voice, so concurrent voices' noise does not correlate
				// into a comb.
				var voice = new ProjectileVoice(i + 1, rate);
				_all.Add(voice);
				_free.Push(voice);
			}
			Log.Debug("projectile voice pool ready (" + total + " voices)");
		}

		/// <summary>A stopped voice the caller then drives with Play, or null (logged) when
		/// the pool is empty.</summary>
		public ProjectileVoice Acquire() {
			// Reclaim released voices whose engine voice has finished draining.
			for (int i = _draining.Count - 1; i >= 0; i--) {
				if (!_draining[i].IsDrained) continue;
				_free.Push(_draining[i]);
				_draining.RemoveAt(i);
			}
			if (_free.Count == 0) {
				Log.Warn("projectile voice pool exhausted (" + _all.Count + " voices); a projectile was dropped");
				return null;
			}
			return _free.Pop();
		}

		/// <summary>Stop a voice and return it to the pool once its engine voice has
		/// drained. A double release is harmless.</summary>
		public void Release(ProjectileVoice voice) {
			if (voice == null) return;
			voice.Stop();
			if (!_draining.Contains(voice) && !_free.Contains(voice)) _draining.Add(voice);
		}
	}
}
