using HandOfFateAccess.Localization;
using HandOfFateAccess.UI;

namespace HandOfFateAccess.Screens {
	/// <summary>
	/// Composes the spoken line for one map card under the free-roam cursor. A sighted
	/// player takes in the card, whether it is where they stand, done, a move they can make
	/// now or still locked, and which ways the board continues, all at a glance; this builds
	/// the same from a MapCellInfo.
	///
	/// Order follows the distinguishing-word-first rule: the card (the content that varies
	/// most) first, then the one state word, then the exits (the directions the cursor can
	/// step), so the player learns the board's shape as they walk it.
	/// </summary>
	public static class MapCellReadout {
		public static string Compose(MapCellInfo c) {
			if (c == null)
				return "";

			var message = new Message();
			if (c.Slot != null)
				message.Add(new MapSlotElement(c.Slot).Describe().Resolve());
			message.Add(StateWord(c));
			message.Add(MapSlotElement.ExitsText(c.Exits));
			return message.Resolve();
		}

		// The single state word, in priority order: standing on it ("here") wins; a completed
		// slot emits no word because its card already reads "completed" (a complete encounter
		// is always face-up), so repeating it would be redundant; otherwise a reachable move
		// now (unlocked and adjacent to the player), then a still-locked slot, else an
		// unlocked slot not adjacent so not a move yet.
		private static string StateWord(MapCellInfo c) {
			if (c.IsPlayerHere)
				return Strings.MapCellHere;
			if (c.IsComplete)
				return "";
			if (c.IsUnlocked && c.IsAdjacentToPlayer)
				return Strings.MapCellReachable;
			if (!c.IsUnlocked)
				return Strings.MapCellLocked;
			return Strings.MapCellOpen;
		}

	}
}
