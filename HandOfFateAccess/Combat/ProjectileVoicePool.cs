using System.Collections.Generic;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// A fixed pool of <see cref="ProjectileVoice"/> objects, each owning a backend synth stream
	/// it generates into. The loop is synthesized, not played from a clip, so the pool needs no
	/// source PCM; it only hands each voice a distinct key and noise seed at construction.
	///
	/// Exhaustion is the silent-failure surface here, so <see cref="Acquire"/> logs and returns
	/// null when every voice is busy rather than dropping a threat without trace.
	/// </summary>
	internal sealed class ProjectileVoicePool {
		private readonly List<ProjectileVoice> _all;
		private readonly Stack<ProjectileVoice> _free;

		public ProjectileVoicePool(int count) {
			_all = new List<ProjectileVoice>(count);
			_free = new Stack<ProjectileVoice>(count);

			// Generate at the backend's mixer rate so it resamples nothing and the tumble sounds
			// exactly as it did when Unity ran the synth at the device rate.
			int rate = AudioEngine.OutputSampleRate > 0 ? AudioEngine.OutputSampleRate : 44100;

			for (int i = 0; i < count; i++) {
				// Distinct seed and key per voice: the seed decorrelates their noise, the key gives
				// each its own backend synth stream.
				var voice = new ProjectileVoice("hofaccess_projectile_" + i, i + 1, rate);
				_all.Add(voice);
				_free.Push(voice);
			}
			Log.Debug("projectile voice pool ready (" + count + " voices)");
		}

		/// <summary>A stopped voice the caller then drives with Play, or null (logged) when
		/// the pool is empty.</summary>
		public ProjectileVoice Acquire() {
			if (_free.Count == 0) {
				Log.Warn("projectile voice pool exhausted (" + _all.Count + " voices); a projectile was dropped");
				return null;
			}
			return _free.Pop();
		}

		/// <summary>Stop a voice and return it to the pool. A double release is harmless.</summary>
		public void Release(ProjectileVoice voice) {
			if (voice == null) return;
			voice.Stop();
			if (!_free.Contains(voice)) _free.Push(voice);
		}
	}
}
