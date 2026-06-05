using System;
using System.Reflection;
using HandOfFateAccess.Util;
using HarmonyLib;

namespace HandOfFateAccess.Patching {
	/// <summary>
	/// Manual Harmony patching with per-patch success/failure logging. We never
	/// PatchAll: that swallows a target that fails to resolve (renamed/removed by a
	/// game update) and degrades silently. Each Patch call logs the outcome so a
	/// broken patch is visible in the log rather than just absent at runtime.
	/// </summary>
	public sealed class HarmonyPatcher {
		private readonly Harmony _harmony;

		public HarmonyPatcher(Harmony harmony) {
			_harmony = harmony;
		}

		/// <summary>
		/// Resolve <paramref name="methodName"/> on <paramref name="targetType"/>
		/// (overload disambiguated by <paramref name="parameters"/>) and attach the
		/// given prefix/postfix. Returns whether the patch was applied.
		/// </summary>
		public bool Patch(Type targetType, string methodName, Type[] parameters, MethodInfo prefix, MethodInfo postfix) {
			string label = targetType.Name + "." + methodName;
			try {
				MethodInfo original = AccessTools.Method(targetType, methodName, parameters);
				if (original == null) {
					Log.Error("patch target not found: " + label);
					return false;
				}

				_harmony.Patch(original,
					prefix == null ? null : new HarmonyMethod(prefix),
					postfix == null ? null : new HarmonyMethod(postfix));
				Log.Info("patched " + label);
				return true;
			} catch (Exception e) {
				Log.Error("patch failed for " + label + ": " + e);
				return false;
			}
		}
	}
}
