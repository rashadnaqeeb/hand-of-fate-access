using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Tracks whether the player is actually translating this frame, by measuring the
	/// combat controller's world displacement. The walk animation keeps cycling, and
	/// firing footstep animation events, while the player is held against a wall, so the
	/// game plays footsteps for movement that is not happening. To a blind player that is
	/// a false "you moved" cue; the footstep patch reads <see cref="IsMoving"/> to drop
	/// those phantom steps. Pumped once per frame from the update loop.
	///
	/// The last position is the only retained value, and only to form a per-frame speed;
	/// the position itself is re-read live every frame, so this holds no stale game state.
	/// </summary>
	internal static class PlayerMotion {
		// Speed, in world units per second, below which the player counts as stationary.
		// Free walk speed is about 10; a wall-blocked player is about 0, so a low cut
		// separates "blocked" from any real movement, including sliding along a wall.
		private const float MovingSpeed = 1f;

		private static Vector3 _lastPosition;
		private static bool _hasLast;
		private static bool _blockedStep;

		public static bool IsMoving { get; private set; }

		/// <summary>Records that a footstep fired while the player was not translating, i.e.
		/// a step taken walking into a wall. Set from the footstep patch, drained once by the
		/// collision cue's pump.</summary>
		public static void NoteBlockedStep() => _blockedStep = true;

		/// <summary>Returns and clears the blocked-step flag.</summary>
		public static bool ConsumeBlockedStep() {
			bool v = _blockedStep;
			_blockedStep = false;
			return v;
		}

		public static void Pump() {
			PlayerController player = PlayerController.Instance;
			if (player == null) {
				_hasLast = false;
				IsMoving = false;
				return;
			}

			Vector3 position = player.transform.position;
			if (!_hasLast) {
				_lastPosition = position;
				_hasLast = true;
				IsMoving = false;
				return;
			}

			float dt = Time.deltaTime;
			float speed = dt > 0f ? Vector3.Distance(position, _lastPosition) / dt : 0f;
			_lastPosition = position;
			IsMoving = speed >= MovingSpeed;
		}
	}
}
