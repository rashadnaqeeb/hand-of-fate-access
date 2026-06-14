using System;
using HandOfFateAccess.Combat;
using HandOfFateAccess.Util;

namespace HandOfFateAccess.Patches {
	/// <summary>
	/// Freezes the combat camera's yaw after the game finishes placing it. UpdateCombatCamera
	/// is the private per-frame combat path inside PlayerCamera.LateUpdate; running as its
	/// postfix means the game's framing pipeline (follow, zoom, the targeting orbit) has
	/// already run and we override only the final rotation. LateUpdate is the only place to
	/// win this, since the camera resets its transform here every frame; setting it from an
	/// Update pump would be stomped. See <see cref="CameraLock"/> for why the lock matters.
	/// </summary>
	internal static class PlayerCamera_UpdateCombatCamera_Patch {
		private static void Postfix(PlayerCamera __instance) {
			try {
				CameraLock.Apply(__instance);
			} catch (Exception ex) {
				Log.Error("camera lock failed: " + ex);
			}
		}
	}
}
