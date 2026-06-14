using System;
using System.IO;
using HandOfFateAccess.Audio;
using HandOfFateAccess.Util;
using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// A short bump panned toward the impact, played while the player keeps walking into a
	/// wall. The footstep patch detects the moment a step fires while the player is not
	/// translating, i.e. pushing into geometry the NavMesh will not let them cross; it
	/// swallows that step and flags it on <see cref="PlayerMotion"/>.
	///
	/// The first blocked step of a contact is silent: the wall tone has already swelled to
	/// full there, so the touch is covered, and a single tap on every glancing brush would
	/// be noise. Each further blocked step while still grinding into the same wall bumps, so
	/// a player wasting movement against geometry keeps hearing it; walking off the wall (any
	/// real movement) ends the contact, and the next fresh collision is again silent on its
	/// first step.
	///
	/// The pan is the left/right component of the player's facing in the combat camera's
	/// frame: the player faces the way they are walking, so that is the direction of the
	/// collision, and it matches the wall tones' camera-relative panning. A head-on (fore
	/// or aft) collision sits near centre, where the corresponding wall tone already carries
	/// the direction.
	/// </summary>
	internal sealed class CollisionCue {
		private const string Key = WallToneComposer.CollisionKey;
		// Full gain, well above the wall tones' ceiling: the transient is an event the player
		// must not miss over the continuous bed and the game's own mix. The clip's authored
		// peak (0.7) leaves this clip-free; renormalizing the wav is the remaining headroom.
		private const float Volume = 1f;

		private bool _loaded;
		private bool _inContact;

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
				_inContact = false;
				return;
			}

			// Real movement means the player has left the wall, so the next collision is a
			// fresh contact whose first blocked step is again silent.
			if (PlayerMotion.IsMoving) _inContact = false;

			bool blockedStep = PlayerMotion.ConsumeBlockedStep();
			if (!blockedStep) return;

			// The first blocked step opens a contact silently (the wall tone already swelled
			// to full there); only continued grinding into the same wall bumps.
			if (!_inContact) {
				_inContact = true;
				return;
			}

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
