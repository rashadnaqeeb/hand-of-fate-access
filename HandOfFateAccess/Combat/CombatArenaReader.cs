using UnityEngine;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The adapter for the wall-tone feature: it measures how far the player can walk in
	/// each direction and hands back a plain <see cref="WallProbe"/>, doing no formatting.
	/// Per the adapter/composition split, deciding what the player hears stays in Core's
	/// <see cref="WallToneComposer"/>; this only measures.
	///
	/// The measurement is a NavMesh raycast, not a physics raycast, because the player is
	/// moved with NavMeshAgent.Move (see ActionMove) and so is clamped to the walkable
	/// surface: what stops the player at a wall is the edge of the NavMesh, not a collider.
	/// A NavMesh.Raycast from the player to a point <see cref="WallToneComposer.Range"/>
	/// away returns the distance to that edge, which is exactly "how far can I walk this
	/// way before something blocks me". This naturally voices the arena's walls and columns
	/// while ignoring props and decoration the player can simply walk past, and needs no
	/// layer mask or destructible filtering.
	///
	/// The four rays leave the player in the combat camera's screen frame (camera-right,
	/// -right, forward, back), so "right" is the direction pushing the stick right would
	/// move the player, not a world compass point.
	/// </summary>
	internal static class CombatArenaReader {
		/// <summary>
		/// Scan all four sides out to <paramref name="maxDistance"/>. Null when combat is
		/// not live (no player or camera in the scene), which the caller treats as "stop
		/// every tone".
		/// </summary>
		public static WallProbe Probe(float maxDistance) {
			PlayerController player = PlayerController.Instance;
			PlayerCamera camera = PlayerCamera.Instance;
			if (player == null || camera == null) return null;

			// The agent's own area mask, so the boundary we measure is the one the player is
			// actually held inside (water and other off-limits areas count as edges too).
			NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
			int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;

			Vector3 origin = player.transform.position;
			Transform cam = camera.transform;
			Vector3 forward = FlattenScreenForward(cam);
			Vector3 right = Flatten(cam.right, forward);

			var distance = new float[4];
			distance[(int)WallSide.Right] = Cast(origin, right, maxDistance, areaMask);
			distance[(int)WallSide.Left] = Cast(origin, -right, maxDistance, areaMask);
			distance[(int)WallSide.Above] = Cast(origin, forward, maxDistance, areaMask);
			distance[(int)WallSide.Below] = Cast(origin, -forward, maxDistance, areaMask);
			return new WallProbe(distance);
		}

		// Distance to the NavMesh edge along dir, or PositiveInfinity if the player could
		// walk the full maxDistance that way unobstructed.
		private static float Cast(Vector3 origin, Vector3 dir, float maxDistance, int areaMask) {
			Vector3 target = origin + dir * maxDistance;
			if (NavMesh.Raycast(origin, target, out NavMeshHit hit, areaMask))
				return hit.distance;
			return float.PositiveInfinity;
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
