using System;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// A short bump played once each time the player walks into a wall, panned toward the
	/// impact. The footstep patch detects the moment a step fires while the player is not
	/// translating, i.e. pushing into geometry the NavMesh will not let them cross; it
	/// swallows that step and flags it on <see cref="PlayerMotion"/>.
	///
	/// One bump per impact, not a stream while held: the cue arms whenever the player is
	/// actually moving and disarms when it fires, so a continuous push gives a single bump
	/// and only real movement (walking off the wall and back, sliding into a fresh corner)
	/// re-arms it.
	///
	/// The pan is the left/right component of the player's facing in the combat camera's
	/// frame: the player faces the way they are walking, so that is the direction of the
	/// collision, and it matches the wall tones' camera-relative panning. A head-on (fore
	/// or aft) collision sits near centre, where the corresponding wall tone already carries
	/// the direction.
	/// </summary>
	internal sealed class CollisionCue {
		private const string Key = WallToneComposer.CollisionKey;
		// Slightly above the wall tones' ceiling so the transient reads as an event over the
		// continuous bed rather than blending into it.
		private const float Volume = 0.7f;

		private bool _loaded;
		private bool _armed;

		public void Initialize(string pluginDir) {
			if (!AudioEngine.IsAvailable) return;
			string path = Path.Combine(Path.Combine(pluginDir, "sounds"), Key + ".wav");
			try {
				byte[] bytes = File.ReadAllBytes(path);
				WavAudio.Decode(bytes, out float[] pcm, out int channels, out int sampleRate);
				AudioEngine.Register(Key, pcm, channels, sampleRate);
				_loaded = true;
			} catch (Exception ex) {
				Log.Error("collision cue failed to load from " + path + ": " + ex);
			}
		}

		public void Pump() {
			if (!_loaded || !AudioEngine.IsAvailable) return;

			// Outside a live fight the cue is idle: a blocked step pending from the fight's
			// last frames is dropped, not held to fire its bump into the pause menu or the
			// post-combat resolution. The Dealer's missile quick-time counts as outside
			// (the player is teleported, not walking).
			if (!CombatGate.IsLive || DealerQte.IsActive) {
				PlayerMotion.ConsumeBlockedStep();
				_armed = false;
				return;
			}

			// Real movement re-arms the cue, so the next time the player jams into a wall it
			// fires afresh; while pinned (never moving) it stays disarmed after the first hit.
			if (PlayerMotion.IsMoving) _armed = true;

			bool blockedStep = PlayerMotion.ConsumeBlockedStep();
			if (!blockedStep || !_armed) return;
			_armed = false;

			PlayerController player = PlayerController.Instance;
			PlayerCamera camera = PlayerCamera.Instance;
			if (player == null || camera == null) return;

			AudioEngine.PlayOneShot(Key, new SoundParams(CollisionPan(player, camera), 1f, Volume));
		}

		// The player's facing is the way they are walking, so its right-component in the
		// camera frame is the collision's left/right position. Both vectors are flattened to
		// the horizontal plane; a degenerate (near-zero) projection falls back to centre.
		private static float CollisionPan(PlayerController player, PlayerCamera camera) {
			Vector3 forward = player.transform.forward;
			forward.y = 0f;
			Vector3 right = camera.transform.right;
			right.y = 0f;
			if (forward.sqrMagnitude < 1e-4f || right.sqrMagnitude < 1e-4f) return 0f;
			return Mathf.Clamp(Vector3.Dot(forward.normalized, right.normalized), -1f, 1f);
		}
	}
}
