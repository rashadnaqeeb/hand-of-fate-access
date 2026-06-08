using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// TEMPORARY debug harness for verifying projectile sonification without needing a ranged
	/// enemy. F9 toggles a synthetic projectile that orbits the player at a fixed radius,
	/// voiced through the same <see cref="ProjectileSonifier.Compose"/> and pitch-voice pool
	/// the real feature uses. The orbit audibly sweeps pan from full right through centre to
	/// full left while the pitch rides its down-biased curve, brightest at due north and
	/// darkest at due south, so the whole bearing mapping is confirmable by ear. It runs on
	/// any screen (the position is synthetic), so no fight is needed. Remove once the feature
	/// is validated against a real shooter.
	/// </summary>
	internal sealed class ProjectileTestSweep {
		private const float Radius = 6f;            // world units from the player, fixed so volume holds steady
		private const float Period = 8f;            // seconds per full orbit
		private const KeyCode Toggle = KeyCode.F9;

		private readonly ProjectileVoicePool _pool;
		private ProjectileVoice _voice;
		private float _angle;                       // radians, 0 = due east, increasing toward north
		private bool _active;

		public ProjectileTestSweep(ProjectileVoicePool pool) {
			_pool = pool;
		}

		public void Pump() {
			if (_pool == null) return;

			if (UnityEngine.Input.GetKeyDown(Toggle)) {
				_active = !_active;
				Log.Info("projectile test sweep " + (_active ? "on" : "off"));
				if (!_active) Stop();
			}
			if (!_active) return;

			_angle += 2f * Mathf.PI / Period * Time.deltaTime;
			float right = Mathf.Cos(_angle) * Radius;
			float forward = Mathf.Sin(_angle) * Radius;
			SoundParams sp = ProjectileSonifier.Compose(right, forward);

			if (_voice != null) {
				_voice.SetParams(sp.Pitch, sp.Pan, sp.Volume);
			} else {
				_voice = _pool.Acquire();
				if (_voice != null) _voice.Play(sp.Pitch, sp.Pan, sp.Volume);
			}
		}

		private void Stop() {
			if (_voice != null) {
				_pool.Release(_voice);
				_voice = null;
			}
		}
	}
}
