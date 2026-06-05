using System.Text;
using System.Text.RegularExpressions;

namespace HandOfFateAccess.Speech {
	/// <summary>
	/// Strips markup from UI text before speech. Hand of Fate's UI is NGUI,
	/// whose rich-text markup is bracket-based BBCode: color tags like [RRGGBB]
	/// and [-], style toggles [b][/b] [i][/i] [u][/u] [s][/s] [c][/c], and
	/// [url=...]display[/url]. A Unity rich-text (&lt;...&gt;) catch-all is kept
	/// as a safety net for labels that use it.
	///
	/// All text must pass through FilterForSpeech before reaching SpeechEngine.
	/// Filter order matters: url inner text is preserved before tags are stripped.
	/// </summary>
	public static class TextFilter {
		// Regexes use no RegexOptions.Compiled: Unity 5.3.7's Mono runtime rejects
		// that flag (ArgumentOutOfRangeException at construction).

		// [url=LINK]display[/url] -> display
		private static readonly Regex UrlTagRegex =
			new Regex(@"\[url\b[^\]]*\](.*?)\[/url\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

		// NGUI color start: 6 or 8 hex digits in brackets, e.g. [FF0000] or [FF0000FF].
		private static readonly Regex ColorStartRegex =
			new Regex(@"\[[0-9a-fA-F]{6}([0-9a-fA-F]{2})?\]");

		// NGUI color end.
		private static readonly Regex ColorEndRegex =
			new Regex(@"\[-\]");

		// NGUI style toggles: [b] [/b] [i] [/i] [u] [/u] [s] [/s] [c] [/c].
		private static readonly Regex StyleTagRegex =
			new Regex(@"\[/?[biusc]\]", RegexOptions.IgnoreCase);

		// Unity rich-text catch-all (bold, color, size, etc.).
		private static readonly Regex UnityRichTextRegex =
			new Regex(@"<[^>]+>");

		// Collapse runs of whitespace (spaces, newlines, tabs) to a single space.
		private static readonly Regex WhitespaceRegex =
			new Regex(@"\s+");

		public static string FilterForSpeech(string text) {
			if (string.IsNullOrEmpty(text)) return "";

			// Strip control characters that can truncate speech output.
			text = StripControlChars(text);
			if (text.Length == 0) return "";

			// Fast path: plain text with no markup delimiters.
			if (text.IndexOf('[') < 0 && text.IndexOf('<') < 0)
				return WhitespaceRegex.Replace(text, " ").Trim();

			text = UrlTagRegex.Replace(text, "$1");
			text = ColorStartRegex.Replace(text, "");
			text = ColorEndRegex.Replace(text, "");
			text = StyleTagRegex.Replace(text, "");
			text = UnityRichTextRegex.Replace(text, "");

			text = WhitespaceRegex.Replace(text, " ");
			return text.Trim();
		}

		private static string StripControlChars(string text) {
			int i = 0;
			for (; i < text.Length; i++) {
				char c = text[i];
				if (c < 0x20 && c != '\n' && c != '\r' && c != '\t')
					break;
			}
			if (i == text.Length) return text;

			var sb = new StringBuilder(text.Length);
			if (i > 0) sb.Append(text, 0, i);
			for (; i < text.Length; i++) {
				char c = text[i];
				if (c >= 0x20 || c == '\n' || c == '\r' || c == '\t')
					sb.Append(c);
			}
			return sb.ToString();
		}
	}
}
