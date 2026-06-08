using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The combat camera's ground frame, shared by the combat audio features: the
	/// player's position plus the on-screen "right" and "forward" (north) directions
	/// flattened onto the horizontal plane. "Right" is the way pushing the stick right
	/// moves the player and "forward" points toward the top of the screen, so a bearing
	/// read off this frame matches what the player feels through the controller, not a
	/// world compass point. Pure extraction off live game objects; no formatting, per the
	/// adapter/composition split.
	/// </summary>
	internal struct CombatFrame {
		public Vector3 Origin;
		public Vector3 Right;
		public Vector3 Forward;

		/// <summary>The frame for this instant, or false when combat is not live (no
		/// player or camera in the scene), which callers treat as "stop every voice".</summary>
		public static bool TryGet(out CombatFrame frame) {
			frame = default(CombatFrame);
			PlayerController player = PlayerController.Instance;
			PlayerCamera camera = PlayerCamera.Instance;
			if (player == null || camera == null) return false;

			Transform cam = camera.transform;
			Vector3 forward = FlattenScreenForward(cam);
			frame.Origin = player.transform.position;
			frame.Forward = forward;
			frame.Right = Flatten(cam.right, forward);
			return true;
		}

		// The combat camera looks down at an angle, so its forward projected onto the
		// horizontal plane is the on-screen "up" direction the player reads movement by.
		// If the camera were ever near top-down, that projection collapses; fall back to
		// the camera's up vector flattened, which points toward the top of the screen.
		private static Vector3 FlattenScreenForward(Transform cam) {
			Vector3 f = cam.forward;
			f.y = 0f;
			if (f.sqrMagnitude < 1e-4f) {
				f = cam.up;
				f.y = 0f;
			}
			return f.normalized;
		}

		private static Vector3 Flatten(Vector3 v, Vector3 fallbackForward) {
			v.y = 0f;
			if (v.sqrMagnitude < 1e-4f)
				return new Vector3(fallbackForward.z, 0f, -fallbackForward.x);
			return v.normalized;
		}
	}
}
