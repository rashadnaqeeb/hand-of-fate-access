namespace HandOfFateAccess.Screens {
	/// <summary>
	/// The distinct game contexts the player can be in. Core deals only in
	/// ScreenId; the plugin maps live game types (GameState subclasses, the
	/// Encounter/Combat/Shop singletons) onto these. Several game states collapse
	/// to one id (the various splash/init states are all Loading) because the
	/// difference is invisible to the player.
	/// </summary>
	public enum ScreenId {
		Unknown,
		Loading,
		MainMenu,
		Intro,
		Attract,
		DeckBuilder,
		DungeonSelect,
		Map,
		CardTable,
		Encounter,
		Combat,
		Shop,
		Dialogue,
		Inventory,
		Paused,
		Results,
		Cabinet,
	}
}
