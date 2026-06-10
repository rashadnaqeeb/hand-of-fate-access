using System;
using System.Collections.Generic;
using System.Reflection;
using HandOfFateAccess.Localization;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// Sweeps every shipped language table for completeness and correctness:
	/// all 12 game locales resolve to a registered table, every string field of
	/// every table carries a speakable value, the format strings keep their
	/// placeholders, and no value smuggles in the characters the translator
	/// rules forbid (screen-reader-hostile punctuation, the game's wrap marker).
	/// A translation that drifted from the StringTable field set cannot pass:
	/// the sweep walks the fields by reflection, so a renamed or added field is
	/// checked in every language automatically.
	/// </summary>
	public class LanguageTableTests : IDisposable {
		public LanguageTableTests() {
			Strings.ResetForTest();
		}

		public void Dispose() {
			Strings.ResetForTest();
		}

		// Code -> factory, mirroring the shipped registry so each table can be
		// inspected directly. The registry itself is verified separately below.
		private static readonly Dictionary<string, Func<StringTable>> Shipped =
			new() {
				{ "en", () => new StringTable() },
				{ "fr", StringTableFr.Create },
				{ "it", StringTableIt.Create },
				{ "de", StringTableDe.Create },
				{ "es", StringTableEs.Create },
				{ "pt-br", StringTablePtBr.Create },
				{ "ru", StringTableRu.Create },
				{ "hu", StringTableHu.Create },
				{ "uk", StringTableUk.Create },
				{ "ja", StringTableJa.Create },
				{ "ko", StringTableKo.Create },
				{ "zh", StringTableZh.Create },
			};

		public static IEnumerable<object[]> AllCodes() {
			foreach (string code in Shipped.Keys)
				yield return new object[] { code };
		}

		private static FieldInfo[] StringFields() =>
			typeof(StringTable).GetFields(BindingFlags.Public | BindingFlags.Instance);

		[Fact]
		public void Every_game_locale_resolves_to_a_registered_table() {
			foreach (string code in Shipped.Keys)
				Assert.True(Strings.SetLanguage(code), $"no table registered for '{code}'");
		}

		[Fact]
		public void Table_fields_are_all_strings() {
			// The sweeps below read every public field as a string; a non-string
			// field added to StringTable would silently escape them.
			foreach (FieldInfo field in StringFields())
				Assert.Equal(typeof(string), field.FieldType);
		}

		[Theory]
		[MemberData(nameof(AllCodes))]
		public void Every_string_is_present_and_speakable(string code) {
			StringTable table = Shipped[code]();
			foreach (FieldInfo field in StringFields()) {
				string value = (string)field.GetValue(table);
				Assert.False(string.IsNullOrEmpty(value), $"{code}: {field.Name} is empty");
				Assert.Equal(value, value.Trim());
				Assert.DoesNotContain('\n', value);
				Assert.DoesNotContain('\r', value);
			}
		}

		[Theory]
		[MemberData(nameof(AllCodes))]
		public void No_string_contains_forbidden_characters(string code) {
			// The translator rules: no em/en dashes or ellipsis (screen readers
			// announce them, breaking flow), no smart quotes, and never the game's
			// "¶" wrap marker, which leaked into some locale text.
			const string forbidden = "¶—–…“”‘’";
			StringTable table = Shipped[code]();
			foreach (FieldInfo field in StringFields()) {
				string value = (string)field.GetValue(table);
				foreach (char ch in forbidden)
					Assert.False(value.IndexOf(ch) >= 0,
						$"{code}: {field.Name} contains forbidden '{ch}': {value}");
			}
		}

		[Theory]
		[MemberData(nameof(AllCodes))]
		public void Format_strings_keep_their_placeholder_and_compose(string code) {
			StringTable table = Shipped[code]();
			Assert.Contains("{0}", table.SliderPercentFormat);
			Assert.Contains("{0}", table.GambitSlotFormat);
			Assert.Contains("{0}", table.PluginLoadedFormat);

			Strings.RegisterForTest("xx-" + code, Shipped[code]);
			Strings.SetLanguage("xx-" + code);
			Assert.Contains("70", Strings.SliderPercent(70));
			Assert.Contains("3", Strings.GambitSlot(3));
			Assert.Contains("0.1.0", Strings.PluginLoaded("0.1.0"));
		}

		[Theory]
		[MemberData(nameof(AllCodes))]
		public void Translations_actually_translate(string code) {
			// Guards against a factory accidentally returning English defaults: a
			// real translation differs from English on most fields (a handful of
			// legitimate cognates like French "Combat" stay identical).
			if (code == "en")
				return;
			var english = new StringTable();
			StringTable table = Shipped[code]();
			int differing = 0;
			foreach (FieldInfo field in StringFields()) {
				if (!string.Equals((string)field.GetValue(table), (string)field.GetValue(english), StringComparison.Ordinal))
					differing++;
			}
			Assert.True(differing > StringFields().Length / 2,
				$"{code}: only {differing} of {StringFields().Length} fields differ from English");
		}
	}
}
