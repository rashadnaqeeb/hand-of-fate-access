namespace HandOfFateAccess.UI {
	/// <summary>
	/// Plain snapshot of a deck-builder pile (Archetype, Equipment, Encounter) as
	/// focused on the select-mode screen, extracted by the plugin's ProxyFactory from
	/// the live DeckBuilderMode. Holds no Unity types so DeckPileElement composition
	/// stays unit-testable.
	///
	/// Only the limited piles (equipment, encounter) carry a count toward a required
	/// limit; the Archetype pile is a single pick with no count, so HasCount is false
	/// and Count/Limit are unset. The insufficient/too-many status is derived from
	/// Count vs Limit by DeckPileElement, mirroring what the game's DeckInfoPanel shows
	/// (its visual flag is exactly count == limit).
	/// </summary>
	public sealed class DeckPileInfo {
		/// <summary>The pile's localized title, e.g. "Equipment". Always present.</summary>
		public string Title { get; }

		/// <summary>True for the limited piles that show a counter; false for Archetype.</summary>
		public bool HasCount { get; }

		/// <summary>Cards currently in the deck. Meaningful only when HasCount.</summary>
		public int Count { get; }

		/// <summary>Cards the deck must hold to be ready. Meaningful only when HasCount.</summary>
		public int Limit { get; }

		/// <summary>A pile with no counter (Archetype / Fates).</summary>
		public DeckPileInfo(string title) {
			Title = title;
			HasCount = false;
		}

		/// <summary>A limited pile with a card count toward its required limit.</summary>
		public DeckPileInfo(string title, int count, int limit) {
			Title = title;
			HasCount = true;
			Count = count;
			Limit = limit;
		}
	}
}
