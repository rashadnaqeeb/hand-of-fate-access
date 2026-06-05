using System.Collections.Generic;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// The routing/diff core for screen context. Holds one base screen (the current
	/// game state) plus an ordered list of overlays (sub-contexts that sit on top of
	/// it -- encounter, combat, shop). The active screen (<see cref="Top"/>) is the
	/// last overlay, or the base when no overlay is active.
	///
	/// Each mutator returns the entry announcement to speak -- the catalog name --
	/// when the resulting top both changed and is announceable, else null. This is
	/// the path-diffing the plan defers from Phase 3's container contexts up to the
	/// screen layer: announce only the context that changed. Popping an overlay that
	/// reveals a different screen beneath re-announces that revealed screen.
	///
	/// The full ordered list (top first) is what Phase 4 input dispatch will walk to
	/// resolve which screen claims a key; this increment only exposes it.
	/// </summary>
	public sealed class ScreenStack {
		private ScreenId _base = ScreenId.Unknown;
		private readonly List<ScreenId> _overlays = new List<ScreenId>();

		/// <summary>The active screen: the top overlay, or the base when none.</summary>
		public ScreenId Top => _overlays.Count > 0 ? _overlays[_overlays.Count - 1] : _base;

		/// <summary>The current base (game-state) screen, ignoring overlays.</summary>
		public ScreenId Base => _base;

		/// <summary>
		/// The active screens from top to base: overlays in reverse push order
		/// followed by the base. A fresh copy, safe for callers to iterate.
		/// </summary>
		public IList<ScreenId> ScreensTopDown() {
			var result = new List<ScreenId>(_overlays.Count + 1);
			for (int i = _overlays.Count - 1; i >= 0; i--)
				result.Add(_overlays[i]);
			result.Add(_base);
			return result;
		}

		/// <summary>
		/// Set the base (game-state) screen. Announces the new top only if the top
		/// actually changed -- setting the base while an overlay is active changes
		/// nothing the player hears.
		/// </summary>
		public string SetBase(ScreenId id) {
			ScreenId oldTop = Top;
			_base = id;
			return AnnouncementOnChange(oldTop);
		}

		/// <summary>Push a sub-context overlay onto the stack.</summary>
		public string PushOverlay(ScreenId id) {
			ScreenId oldTop = Top;
			_overlays.Add(id);
			return AnnouncementOnChange(oldTop);
		}

		/// <summary>
		/// Remove a specific overlay id from the stack (most recent occurrence). A
		/// no-op when it is not present. Announces whatever screen is now on top if
		/// that changed -- including re-announcing the base when the last overlay is
		/// popped.
		/// </summary>
		public string PopOverlay(ScreenId id) {
			ScreenId oldTop = Top;
			int index = _overlays.LastIndexOf(id);
			if (index < 0) return null;
			_overlays.RemoveAt(index);
			return AnnouncementOnChange(oldTop);
		}

		private string AnnouncementOnChange(ScreenId oldTop) {
			ScreenId newTop = Top;
			if (newTop == oldTop) return null;
			return ScreenCatalog.AnnouncementFor(newTop);
		}
	}
}
