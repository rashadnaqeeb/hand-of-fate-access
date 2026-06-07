using System.Collections.Generic;
using HandOfFateAccess.Screens;
using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The free-roam map cursor's per-card readout: the card first, then the one state word
	/// (here / completed / reachable / locked / open) in priority order, then the exits (the
	/// directions holding a neighbouring card the cursor can step to). The cursor only ever
	/// lands on cards, so there is no gap case.
	/// </summary>
	public class MapCellReadoutTests {
		private static MapSlotInfo FaceUp(string name, string desc = "", bool complete = false) =>
			new MapSlotInfo(new CardFace(false, new CardInfo(name, desc, "", "", complete: complete)),
				new List<CardFace>());

		private static readonly MapExits NoExits = new MapExits(false, false, false, false);

		[Fact]
		public void Player_cell_reads_card_then_here_then_exits() {
			var c = new MapCellInfo(FaceUp("Entrance", complete: true), isComplete: true, isUnlocked: true,
				isPlayerHere: true, isAdjacentToPlayer: false, new MapExits(true, false, false, true));
			// The card carries its own "completed"; the state word is "here" (the player
			// stands on it), then the directions the cursor can move.
			Assert.Equal("Entrance, completed, here, exits up, right", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Reachable_is_unlocked_and_adjacent_to_player() {
			var c = new MapCellInfo(FaceUp("Goblins"), isComplete: false, isUnlocked: true,
				isPlayerHere: false, isAdjacentToPlayer: true, new MapExits(false, false, true, false));
			Assert.Equal("Goblins, reachable, exits left", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Unlocked_but_not_adjacent_is_open() {
			var c = new MapCellInfo(FaceUp("Shop"), isComplete: false, isUnlocked: true,
				isPlayerHere: false, isAdjacentToPlayer: false, new MapExits(true, true, false, false));
			Assert.Equal("Shop, open, exits up, down", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Locked_card_reads_locked() {
			var c = new MapCellInfo(FaceUp("Bandits"), isComplete: false, isUnlocked: false,
				isPlayerHere: false, isAdjacentToPlayer: true, new MapExits(false, true, false, true));
			Assert.Equal("Bandits, locked, exits down, right", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Completed_card_carries_completed_not_a_state_word() {
			// A finished card adjacent to the player reads its own "completed" and no reachable
			// word.
			var c = new MapCellInfo(FaceUp("Ambush", complete: true), isComplete: true, isUnlocked: true,
				isPlayerHere: false, isAdjacentToPlayer: true, new MapExits(true, false, false, false));
			Assert.Equal("Ambush, completed, exits up", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Exits_list_in_reading_order() {
			var c = new MapCellInfo(FaceUp("Crossroads"), isComplete: false, isUnlocked: false,
				isPlayerHere: false, isAdjacentToPlayer: false, new MapExits(true, true, true, true));
			Assert.Equal("Crossroads, locked, exits up, down, left, right", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Face_down_card_withholds_identity() {
			var down = new MapSlotInfo(new CardFace(true, null), new List<CardFace>());
			var c = new MapCellInfo(down, isComplete: false, isUnlocked: false,
				isPlayerHere: false, isAdjacentToPlayer: true, new MapExits(false, false, false, true));
			Assert.Equal("face down card, locked, exits right", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Card_with_no_slot_data_reads_state_and_exits_only() {
			var c = new MapCellInfo(null, isComplete: false, isUnlocked: true,
				isPlayerHere: false, isAdjacentToPlayer: true, new MapExits(false, false, true, false));
			Assert.Equal("reachable, exits left", MapCellReadout.Compose(c));
		}

		[Fact]
		public void Null_cell_reads_nothing() {
			Assert.Equal("", MapCellReadout.Compose(null));
		}
	}
}
