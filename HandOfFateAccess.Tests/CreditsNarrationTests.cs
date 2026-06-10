using System.Collections.Generic;
using HandOfFateAccess.Screens;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The credits section line: title first, then each role and name in display
	/// order. Composed by plain join (not Message) so a legitimately repeated role
	/// or name is kept, not collapsed as a duplicate.
	/// </summary>
	public class CreditsNarrationTests {
		private static string Compose(string title, params CreditsEntry[] entries) =>
			CreditsNarration.ComposeSection(new CreditsSection(title, new List<CreditsEntry>(entries)));

		[Fact]
		public void Title_then_roles_and_names_in_order() {
			Assert.Equal("Defiant Development, Design, Morgan Jaffit, Kim Allom",
				Compose("Defiant Development",
					new CreditsEntry("Design", "Morgan Jaffit"),
					new CreditsEntry(null, "Kim Allom")));
		}

		[Fact]
		public void Empty_title_and_blank_fields_dropped() {
			Assert.Equal("Alice, Bob",
				Compose(null,
					new CreditsEntry("", "Alice"),
					new CreditsEntry(null, "Bob")));
		}

		[Fact]
		public void Repeated_names_are_kept() {
			// Two backers with the same name are both real entries; Message dedup
			// would have collapsed them, which is why this composes by plain join.
			Assert.Equal("Backers, Sam Smith, Sam Smith",
				Compose("Backers",
					new CreditsEntry(null, "Sam Smith"),
					new CreditsEntry(null, "Sam Smith")));
		}

		[Fact]
		public void Empty_section_is_empty() {
			Assert.Equal("", Compose(null));
		}
	}
}
