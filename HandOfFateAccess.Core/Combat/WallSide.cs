namespace HandOfFateAccess.Combat {
	/// <summary>
	/// The four directions a wall tone probes, named in the player's screen frame
	/// (the combat camera's horizontal axes), not world compass points. Right/Left
	/// are the camera's right/left, so a wall the player would reach by pushing the
	/// stick that way pans to that ear; Above/Below are the camera's forward/back,
	/// which carry no pan. Each maps to one of the four authored tone clips.
	/// </summary>
	public enum WallSide {
		Right,
		Left,
		Above,
		Below,
	}
}
