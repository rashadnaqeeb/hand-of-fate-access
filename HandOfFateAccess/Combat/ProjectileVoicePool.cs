using System.Collections.Generic;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// A fixed pool of <see cref="ProjectileVoice"/> objects, each a child GameObject with
	/// its own AudioSource and synthesis callback. The projectile loop is generated, not
	/// played from a clip, so the pool needs no source PCM; it only shares one silent looping
	/// clip that keeps each voice's filter callback firing.
	///
	/// Exhaustion is the silent-failure surface here, so <see cref="Acquire"/> logs and
	/// returns null when every voice is busy rather than dropping a threat without trace.
	/// </summary>
	internal sealed class ProjectileVoicePool {
		private readonly List<ProjectileVoice> _all;
		private readonly Stack<ProjectileVoice> _free;

		public ProjectileVoicePool(int count) {
			_all = new List<ProjectileVoice>(count);
			_free = new Stack<ProjectileVoice>(count);

			var root = new GameObject("HoFAccess_ProjectileVoices");
			ScenePersistence.Protect(root);

			// One silent looping clip keeps every voice's AudioSource "playing" so its filter
			// callback fires; the callback overwrites the silence with synthesized audio.
			int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
			AudioClip silence = AudioClip.Create("hofaccess_silence", 2048, 1, rate, false);

			for (int i = 0; i < count; i++) {
				var go = new GameObject("ProjectileVoice" + i);
				ScenePersistence.Protect(go);
				go.transform.parent = root.transform;
				var voice = go.AddComponent<ProjectileVoice>();
				voice.Init(i + 1, silence);   // distinct seed per voice so their noise does not correlate
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
