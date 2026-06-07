namespace HandOfFateAccess.Input {
	/// <summary>
	/// A four-way grid direction for the map cursor, named in screen terms (the movement
	/// keys). The cursor maps these onto the board's grid axes, which match the game's own
	/// navigation (up is one row toward the top, right is one column right).
	/// </summary>
	internal enum Direction {
		Up,
		Down,
		Left,
		Right
	}
}
