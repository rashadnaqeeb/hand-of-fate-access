using System.Collections.Generic;
using HandOfFateAccess.Speech;

namespace HandOfFateAccess.UI {
	/// <summary>
	/// A spoken announcement built from ordered raw text parts. Parts are added in
	/// the order they should be read; markup stripping, empty-dropping, and
	/// duplicate collapsing all happen at Resolve time, never as parts go in. This
	/// is the reusable composition kernel: every UIElement builds its line through
	/// a Message rather than joining strings by hand.
	///
	/// Dedup collapses NGUI shadow/outline copies (identical filtered text) and any
	/// field a proxy happens to repeat; the screen-reader user never wants the same
	/// fragment twice in one line.
	/// </summary>
	public sealed class Message {
		private readonly List<string> _parts = new List<string>();

		/// <summary>Append a raw (markup-intact) part. Null/empty is ignored.</summary>
		public Message Add(string raw) {
			if (!string.IsNullOrEmpty(raw))
				_parts.Add(raw);
			return this;
		}

		/// <summary>Append several raw parts in order.</summary>
		public Message AddRange(IEnumerable<string> raws) {
			if (raws != null)
				foreach (string raw in raws)
					Add(raw);
			return this;
		}

		/// <summary>
		/// Filter each part for speech, drop empties, collapse duplicates, and join
		/// with the separator. Returns "" when nothing usable remains.
		/// </summary>
		public string Resolve(string separator = ", ") {
			var kept = new List<string>();
			var seen = new HashSet<string>();
			foreach (string raw in _parts) {
				string filtered = TextFilter.FilterForSpeech(raw);
				if (string.IsNullOrEmpty(filtered)) continue;
				if (!seen.Add(filtered)) continue;
				kept.Add(filtered);
			}
			return string.Join(separator, kept.ToArray());
		}
	}
}
