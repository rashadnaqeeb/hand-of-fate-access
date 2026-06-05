using HandOfFateAccess.Speech;
using Xunit;

namespace HandOfFateAccess.Tests {
	public class TextFilterTests {
		[Theory]
		// Empty / null
		[InlineData(null, "")]
		[InlineData("", "")]
		[InlineData("   ", "")]
		// Plain text passes through, whitespace normalized and trimmed
		[InlineData("Hello world", "Hello world")]
		[InlineData("  Hello world  ", "Hello world")]
		[InlineData("a\n\nb\t c", "a b c")]
		// NGUI color tags (6 and 8 hex) stripped, end tag stripped
		[InlineData("[FF0000]Red[-]", "Red")]
		[InlineData("[ff0000]Red[-]", "Red")]
		[InlineData("[FF0000FF]Red[-]", "Red")]
		// NGUI style toggles stripped (case-insensitive)
		[InlineData("[b]Bold[/b]", "Bold")]
		[InlineData("[B]Bold[/B]", "Bold")]
		[InlineData("[i]a[/i][u]b[/u][s]c[/s][c]d[/c]", "abcd")]
		// NGUI url keeps display text
		[InlineData("[url=http://x.com]click here[/url]", "click here")]
		// Unity rich text stripped
		[InlineData("<b>bold</b>", "bold")]
		[InlineData("<color=#ff0000>red</color>", "red")]
		// Mixed markup
		[InlineData("[FFAA00]Gold:[-] [b]5[/b]", "Gold: 5")]
		[InlineData("[b]a[/b]\n[b]b[/b]", "a b")]
		// Markup-only collapses to empty
		[InlineData("[b][/b]", "")]
		[InlineData("[FF0000][-]", "")]
		// Real (non-tag) brackets are preserved
		[InlineData("deal 5 [50%] damage", "deal 5 [50%] damage")]
		[InlineData("loot [chest]", "loot [chest]")]
		public void FilterForSpeech_Cases(string input, string expected) {
			Assert.Equal(expected, TextFilter.FilterForSpeech(input));
		}

		[Fact]
		public void FilterForSpeech_StripsControlChars() {
			// Build control chars in code to avoid string-escape ambiguity.
			Assert.Equal("abcdef", TextFilter.FilterForSpeech("abc" + (char)0 + "def"));
			Assert.Equal("hello", TextFilter.FilterForSpeech((char)1 + "hello"));
		}

		[Fact]
		public void FilterForSpeech_KeepsNewlineTabAsWhitespace() {
			// \n, \r and \t are not stripped as control chars; they collapse to a space.
			Assert.Equal("a b", TextFilter.FilterForSpeech("a\r\nb"));
			Assert.Equal("a b", TextFilter.FilterForSpeech("a\tb"));
		}
	}
}
