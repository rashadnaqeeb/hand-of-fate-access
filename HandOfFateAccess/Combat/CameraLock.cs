using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Combat {
	/// <summary>
	/// Freezes the combat camera's yaw so "up on the stick" maps to one fixed world
	/// direction for the whole fight. The game derives the player's movement, the
	/// attack-aim cone, and the mod's own audio bearing from the live camera matrix, so
	/// the camera orbiting to keep enemies framed spins the player's entire spatial frame
	/// under them: a wall that read as straight ahead a moment ago now reads off to one
	/// side, and the mental map keeps rotating. Re-pinning the yaw stabilizes all three at
	/// once. Nothing in game logic reads camera yaw except that input mapping, so freezing
	/// it costs only the visual reframing, which this mod's player never sees.
	///
	/// The game's own framing pipeline still runs every LateUpdate; this overrides only
	/// its result, after it. Position keeps following the player and distance keeps zooming
	/// (both read back unchanged), so only rotation is frozen. The lock yields whenever a
	/// scripted camera takeover owns the view (the Dealer missile quick-time, any override
	/// blend) so those play unaltered, and re-captures north the next time a fight starts.
	/// </summary>
	internal static class CameraLock {
		private static bool s_captured;
		private static float s_yaw;
		private static float s_pitch;

		// m_overrideCamera is non-null while a scripted camera takeover is blending (the
		// missile event); m_yaw is the game's stored yaw, written back so any later read
		// matches the frozen camera rather than the angle the pipeline computed.
		private static readonly FieldInfo OverrideCameraField =
			AccessTools.Field(typeof(PlayerCamera), "m_overrideCamera");
		private static readonly FieldInfo YawField =
			AccessTools.Field(typeof(PlayerCamera), "m_yaw");

		/// <summary>Re-pin the camera to the frozen yaw. Called from the post-pipeline
		/// postfix, so the game has already placed the camera for this frame.</summary>
		public static void Apply(PlayerCamera camera) {
			// Outside a live fight, or while the missile event owns the player and camera,
			// drop the lock so the next fight re-picks north from its own arena angle.
			if (!CombatGate.IsLive || DealerQte.IsActive || OverrideCameraField.GetValue(camera) != null) {
				s_captured = false;
				return;
			}

			Transform t = camera.transform;
			if (!s_captured) {
				// Wait for the level-in (or any) blend to settle before freezing, so north
				// is the arena's resting angle and not a half-finished transition. Pitch is
				// frozen with it, holding the downward tilt steady so the framing does not bob.
				if (camera.IsActiveCameraBlending) return;
				Vector3 e = t.eulerAngles;
				s_yaw = e.y;
				s_pitch = e.x;
				s_captured = true;
			}

			Quaternion rot = Quaternion.Euler(s_pitch, s_yaw, 0f);
			float dist = camera.CurrentDistance;
			Vector3 target = camera.TargetPosition;
			t.rotation = rot;
			t.position = target - rot * Vector3.forward * dist;

			// Re-place the audio listener behind the camera along its forward, the same
			// placement UpdateCombatCamera makes, so the game's own combat SFX pan from the
			// frozen orientation too.
			AudioListener listener = camera.Listener;
			if (listener != null)
				listener.transform.position = t.position + t.forward * (dist - camera.ActiveCamera.ListenerDistance);

			YawField.SetValue(camera, s_yaw);
		}
	}
}
