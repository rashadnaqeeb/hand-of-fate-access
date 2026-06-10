# Translation subagent prompt template

One subagent per language. Fill the {PLACEHOLDERS}, paste the vocab sheet from
`game-locales/vocab/{code}.txt` and the full current `StringTable.cs` source at
the bottom. The subagent needs no repo access beyond writing its one output
file; everything it needs is in the prompt.

Class name / file name per code: fr=Fr, it=It, de=De, es=Es, pt-br=PtBr,
ru=Ru, hu=Hu, uk=Uk, ja=Ja, ko=Ko, zh=Zh.

Title rendering: zh uses the game's official 命运之手; ja uses ハンドオブフェイト;
every other language keeps the Latin "Hand of Fate" (verified against each
locale's own text).

---

You are translating the authored spoken strings of Hand of Fate Access, a mod
that makes the card game Hand of Fate playable by blind players. A screen
reader speaks these strings; they are never displayed. Translate from English
into {LANGUAGE} ({code}).

Deliverable: write exactly one file,
`C:\Users\rasha\Documents\hand of fate\translation-work\StringTable.{Code}.cs`,
containing only this (real, compiling C# 7.3, tab indentation):

```csharp
namespace HandOfFateAccess.Localization {
	/// <summary>{LANGUAGE} ({code}) authored-string table. The English source of
	/// truth and the translator notes live in StringTable.</summary>
	internal static class StringTable{Code} {
		internal static StringTable Create() {
			return new StringTable {
				ScreenLoading = "...",
				// ... every field of StringTable, in source order ...
			};
		}
	}
}
```

Rules:
- Read the translator notes at the top of the source below, and the comment on
  each string: they say where each line is spoken and what it must convey.
- Set EVERY field explicitly, in the order the source declares them, even when
  your translation happens to match the English.
- Keep each "{0}" placeholder exactly as written; you may move it within the
  sentence to fit {LANGUAGE} word order.
- The audience is expert screen-reader users: short and information-dense, no
  politeness or filler, never drop information the English carries. Where
  {LANGUAGE} grammar forces a choice, prefer keeping the distinguishing word
  as early in the line as possible.
- Plain punctuation only: no em dashes, no smart quotes, no ellipsis
  character, and never the "¶" character some game text contains (it is a
  text-wrapping hint for the game's renderer, not language).
- Use the game vocabulary sheet below for every word it covers, so the mod's
  speech matches the words the game itself uses in {LANGUAGE}. If a vocabulary
  word is clearly wrong for a context, diverge and flag it in your reply.
- The game's title in {LANGUAGE} is {TITLE_RENDERING} (for PluginLoadedFormat).
- Escape any double quote inside a value as \" and keep each value on one line.

Reply with only a short list of decisions worth flagging: ambiguities, places
you diverged from the vocabulary sheet and why, anything you could not
translate confidently. Do not repeat the file content in your reply.

# Game vocabulary (English => {LANGUAGE}), from the game's own locale files
{VOCAB}

# Source: HandOfFateAccess.Core/Localization/StringTable.cs
{SOURCE}
