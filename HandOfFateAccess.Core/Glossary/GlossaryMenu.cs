namespace HandOfFateAccess.Glossary {
	/// <summary>
	/// The glossary's cursor over its entries: a flat list that wraps at both ends,
	/// like the game's own menus. The plugin owns when to move it and speaks the
	/// entry it returns; this only decides which entry the player is on, so the
	/// navigation behavior is testable off-engine.
	/// </summary>
	public sealed class GlossaryMenu {
		private readonly GlossaryEntry[] _entries;
		private int _index;

		public GlossaryMenu(GlossaryEntry[] entries) {
			_entries = entries;
		}

		public GlossaryEntry Current => _entries[_index];

		public GlossaryEntry MoveNext() {
			_index = (_index + 1) % _entries.Length;
			return Current;
		}

		public GlossaryEntry MovePrevious() {
			_index = (_index - 1 + _entries.Length) % _entries.Length;
			return Current;
		}

		/// <summary>Back to the first entry; each open starts at the top so the list
		/// reads the same way every time.</summary>
		public void Reset() {
			_index = 0;
		}
	}
}
