using HandOfFateAccess.Input;
using HandOfFateAccess.Screens;
using HandOfFateAccess.Speech;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Maps {
	/// <summary>
	/// The free-roam map cursor: a position over the board the player moves independently of
	/// the game's own selection, to survey the whole layout without sight. It inspects only;
	/// the game's selection is left alone (committing a move is a later step). The position
	/// is the mod's own navigation state, not game data, so it is held here; everything it
	/// reads about a cell is re-queried live each move.
	///
	/// Opened anchored on the player's slot each time the map becomes active, so it always
	/// starts from where the player stands. Moving steps to the neighbouring card in the
	/// grid's own axes (up is one row toward the top), matching the game's nav so a later
	/// commit lines up. It walks card to card only: a step toward a gap or the board edge
	/// (no card there) does nothing.
	/// </summary>
	internal sealed class MapCursor {
		private int _x;
		private int _y;
		private bool _anchored;

		/// <summary>
		/// The map became the active screen. Drop any stale anchor from a previous map and
		/// re-anchor on the player. If the player counter is not placed yet this frame, the
		/// cursor stays unanchored and the first move re-anchors once it is.
		/// </summary>
		public void OnMapEntered() {
			_anchored = false;
			AnchorToPlayer();
		}

		/// <summary>Place the cursor on the player's slot. No-op until the board is live.</summary>
		public void AnchorToPlayer() {
			int x, y;
			if (!MapBoardReader.TryGetPlayerCell(out x, out y))
				return;
			_x = x;
			_y = y;
			_anchored = true;
		}

		/// <summary>
		/// Snap the cursor onto the game's own selected slot, so moving the game cursor brings
		/// the free cursor with it. No-op when nothing on the board is selected.
		/// </summary>
		public void AnchorToGameCursor() {
			int x, y;
			if (!MapBoardReader.TryGetSelectedCell(out x, out y))
				return;
			_x = x;
			_y = y;
			_anchored = true;
		}

		public void Move(Direction dir) {
			if (!_anchored) {
				AnchorToPlayer();
				if (!_anchored)
					return;
			}

			int x = _x;
			int y = _y;
			switch (dir) {
				case Direction.Up: y -= 1; break;
				case Direction.Down: y += 1; break;
				case Direction.Left: x -= 1; break;
				case Direction.Right: x += 1; break;
			}

			if (!MapBoardReader.HasSlot(x, y))
				return;

			_x = x;
			_y = y;
			Announce();
		}

		private void Announce() {
			MapCellInfo cell = MapBoardReader.Read(_x, _y);
			if (cell == null)
				return;
			string line = MapCellReadout.Compose(cell);
			if (!string.IsNullOrEmpty(line))
				SpeechPipeline.SpeakInterrupt(line);
		}
	}
}
