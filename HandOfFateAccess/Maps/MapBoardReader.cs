using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using UnityEngine;

namespace HandOfFateAccess.Maps {
	/// <summary>
	/// Extracts the live map board into Unity-free cell snapshots for the free-roam cursor.
	/// The board is a grid of slots (empty grid cells were destroyed at fill, so a missing
	/// slot is a gap in the shape). This reads one cell at call time, reusing MapSlotReader
	/// for the card stack and adding the slot's state and the player's offset; nothing is
	/// cached. Composition into spoken text is MapCellReadout's job, in Core.
	/// </summary>
	internal static class MapBoardReader {
		private static MapLayout Layout {
			get { return Map.Instance != null ? Map.Instance.MapLayout : null; }
		}

		/// <summary>The player counter's grid cell, false when no live player on the board.</summary>
		public static bool TryGetPlayerCell(out int x, out int y) {
			x = 0;
			y = 0;
			Map map = Map.Instance;
			PlayerCounter counter = map != null ? map.PlayerCounter : null;
			if (counter == null || counter.CurrentSlot == null)
				return false;
			Vector2 g = counter.CurrentSlot.GridPosition;
			x = Mathf.RoundToInt(g.x);
			y = Mathf.RoundToInt(g.y);
			return true;
		}

		/// <summary>
		/// The card at this grid position: its card stack plus its state, whether the player
		/// stands on or beside it, and which directions hold a neighbouring card. Null when
		/// no slot is there (a gap) or there is no live layout, neither of which the cursor
		/// lands on.
		/// </summary>
		public static MapCellInfo Read(int x, int y) {
			MapLayout layout = Layout;
			if (layout == null)
				return null;
			MapLayoutSlot slot = layout.GetSlot(new Vector2(x, y));
			if (slot == null)
				return null;

			int px, py;
			bool hasPlayer = TryGetPlayerCell(out px, out py);
			bool here = hasPlayer && px == x && py == y;
			bool adjacent = hasPlayer && System.Math.Abs(px - x) + System.Math.Abs(py - y) == 1;

			// The grid axes match the game's nav: up is one row toward the top (y-1).
			var exits = new MapExits(
				HasSlot(x, y - 1),
				HasSlot(x, y + 1),
				HasSlot(x - 1, y),
				HasSlot(x + 1, y));

			MapSlotInfo cards = MapSlotReader.Read(slot);
			return new MapCellInfo(cards, slot.IsComplete, slot.IsUnlocked, here, adjacent, exits);
		}

		/// <summary>
		/// The grid cell of the game's own currently selected slot, false when nothing on the
		/// board is selected. Used to snap the free cursor onto the game's cursor when the
		/// player moves it.
		/// </summary>
		public static bool TryGetSelectedCell(out int x, out int y) {
			x = 0;
			y = 0;
			GameObject selected = UICamera.selectedObject;
			if (selected == null)
				return false;
			MapLayoutSlot slot = selected.GetComponentInParent<MapLayoutSlot>();
			if (slot == null)
				return false;
			x = Mathf.RoundToInt(slot.GridPosition.x);
			y = Mathf.RoundToInt(slot.GridPosition.y);
			return true;
		}

		/// <summary>Whether a card sits at this grid position (the cursor can step there).</summary>
		public static bool HasSlot(int x, int y) {
			MapLayout layout = Layout;
			return layout != null && layout.GetSlot(new Vector2(x, y)) != null;
		}
	}
}
