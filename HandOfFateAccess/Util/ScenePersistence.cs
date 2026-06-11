using UnityEngine;

namespace HandOfFateAccess.Util {
	/// <summary>
	/// Puts a mod GameObject out of the scene's reach. DontDestroyOnLoad alone is not
	/// enough: the game's reset-progress and switch-user path (Game.ClearScene)
	/// destroys every GameObject FindObjectsOfType returns, DontDestroyOnLoad ones
	/// included, before reloading card_table. FindObjectsOfType skips objects flagged
	/// HideAndDontSave, so the sweep never sees a protected object. Must be applied to
	/// every GameObject the mod creates, children included: the sweep destroys each
	/// object it finds directly, not through its parent.
	/// </summary>
	internal static class ScenePersistence {
		public static void Protect(GameObject go) {
			Object.DontDestroyOnLoad(go);
			go.hideFlags |= HideFlags.HideAndDontSave;
		}
	}
}
