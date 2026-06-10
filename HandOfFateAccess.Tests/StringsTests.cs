using System;
using HandOfFateAccess.Localization;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The language-selection mechanism behind the authored strings: table
	/// resolution by game language code, English fallback for an unknown
	/// language, per-string fallback inside a partial translation, and live
	/// re-resolution after a switch (nothing baked at class load).
	/// </summary>
	public class StringsTests : IDisposable {
		public StringsTests() {
			Strings.ResetForTest();
		}

		public void Dispose() {
			Strings.ResetForTest();
		}

		[Fact]
		public void English_is_the_default() {
			Assert.Equal("en", Strings.ActiveLanguage);
			Assert.Equal("Map", Strings.ScreenMap);
		}

		[Fact]
		public void Unknown_language_falls_back_to_English() {
			Assert.False(Strings.SetLanguage("fr"));
			Assert.Equal("en", Strings.ActiveLanguage);
			Assert.Equal("Map", Strings.ScreenMap);
		}

		[Fact]
		public void Null_and_empty_fall_back_to_English() {
			Assert.False(Strings.SetLanguage(null));
			Assert.Equal("en", Strings.ActiveLanguage);
			Assert.False(Strings.SetLanguage(""));
			Assert.Equal("en", Strings.ActiveLanguage);
		}

		[Fact]
		public void Registered_language_is_selected() {
			Strings.RegisterForTest("xx", () => new StringTable { ScreenMap = "Karte" });
			Assert.True(Strings.SetLanguage("xx"));
			Assert.Equal("xx", Strings.ActiveLanguage);
			Assert.Equal("Karte", Strings.ScreenMap);
		}

		[Fact]
		public void Language_codes_match_case_insensitively() {
			// The game's codes are lowercase ("en", "pt-br"), but the table lookup
			// should not depend on it.
			Strings.RegisterForTest("xx", () => new StringTable { ScreenMap = "Karte" });
			Assert.True(Strings.SetLanguage("XX"));
			Assert.Equal("xx", Strings.ActiveLanguage);
			Assert.Equal("Karte", Strings.ScreenMap);
		}

		[Fact]
		public void Untranslated_string_in_a_partial_table_keeps_English() {
			Strings.RegisterForTest("xx", () => new StringTable { ScreenMap = "Karte" });
			Strings.SetLanguage("xx");
			Assert.Equal("Combat", Strings.ScreenCombat);
		}

		[Fact]
		public void Switching_back_restores_English() {
			Strings.RegisterForTest("xx", () => new StringTable { ScreenMap = "Karte" });
			Strings.SetLanguage("xx");
			Assert.True(Strings.SetLanguage("en"));
			Assert.Equal("Map", Strings.ScreenMap);
		}

		[Fact]
		public void Format_strings_resolve_from_the_active_table() {
			Strings.RegisterForTest("xx", () => new StringTable {
				SliderPercentFormat = "{0} prozent",
				GambitSlotFormat = "Platz {0}",
				PluginLoadedFormat = "Version {0} geladen",
			});
			Strings.SetLanguage("xx");
			Assert.Equal("70 prozent", Strings.SliderPercent(70));
			Assert.Equal("Platz 3", Strings.GambitSlot(3));
			Assert.Equal("Version 0.1.0 geladen", Strings.PluginLoaded("0.1.0"));
		}

		// The two catalogs built once at class load were the stale-language risk:
		// their entries must read through Strings at lookup time, not bake the
		// values of whatever language was active when the class first loaded.

		[Fact]
		public void Screen_catalog_follows_a_language_switch() {
			Assert.Equal("Map", Screens.ScreenCatalog.NameOf(Screens.ScreenId.Map));
			Strings.RegisterForTest("xx", () => new StringTable { ScreenMap = "Karte" });
			Strings.SetLanguage("xx");
			Assert.Equal("Karte", Screens.ScreenCatalog.NameOf(Screens.ScreenId.Map));
		}

		[Fact]
		public void Glossary_catalog_follows_a_language_switch() {
			Strings.RegisterForTest("xx", () => new StringTable { GlossaryBlock = "Block, xx" });
			Strings.SetLanguage("xx");
			Assert.Equal("Block, xx", Glossary.GlossaryCatalog.Entries[0].Label);
		}

		[Fact]
		public void English_format_strings_compose() {
			Assert.Equal("70%", Strings.SliderPercent(70));
			Assert.Equal("Slot 3", Strings.GambitSlot(3));
			Assert.Equal("Hand of Fate Access version 0.1.0 loaded", Strings.PluginLoaded("0.1.0"));
		}
	}
}
