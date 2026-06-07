namespace HandOfFateAccess.UI {
	/// <summary>
	/// Which orthogonal directions from a map cell hold a neighbouring card, i.e. where the
	/// free-roam cursor can step next. Gaps and the board edge are not exits, so the cursor
	/// only ever walks card to card.
	/// </summary>
	public sealed class MapExits {
		public bool Up { get; }
		public bool Down { get; }
		public bool Left { get; }
		public bool Right { get; }

		public MapExits(bool up, bool down, bool left, bool right) {
			Up = up;
			Down = down;
			Left = left;
			Right = right;
		}
	}

	/// <summary>
	/// One card cell of the map board as the free-roam cursor sees it: a plain snapshot
	/// extracted by the plugin's MapBoardReader, holding no Unity types. The cursor only
	/// lands on cards (never gaps), so every cell here is a real slot.
	///
	/// State is carried as raw flags; MapCellReadout decides the single spoken state word
	/// and its priority, so that stays unit-tested. The exits tell the player which ways the
	/// cursor can move from here, in place of an absolute position readout.
	/// </summary>
	public sealed class MapCellInfo {
		/// <summary>The slot's card stack, or null when the slot holds no card.</summary>
		public MapSlotInfo Slot { get; }

		/// <summary>The slot's encounter is finished.</summary>
		public bool IsComplete { get; }

		/// <summary>The slot is unlocked (its lock has opened; not necessarily reachable now).</summary>
		public bool IsUnlocked { get; }

		/// <summary>The player's counter stands on this slot.</summary>
		public bool IsPlayerHere { get; }

		/// <summary>This slot is orthogonally adjacent to the player's slot.</summary>
		public bool IsAdjacentToPlayer { get; }

		/// <summary>Which directions hold a neighbouring card to step to.</summary>
		public MapExits Exits { get; }

		public MapCellInfo(MapSlotInfo slot, bool isComplete, bool isUnlocked,
				bool isPlayerHere, bool isAdjacentToPlayer, MapExits exits) {
			Slot = slot;
			IsComplete = isComplete;
			IsUnlocked = isUnlocked;
			IsPlayerHere = isPlayerHere;
			IsAdjacentToPlayer = isAdjacentToPlayer;
			Exits = exits;
		}
	}
}
