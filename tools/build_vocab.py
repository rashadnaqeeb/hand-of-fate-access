"""Build per-language game-vocabulary sheets for the translation subagents.

Reads the locale dictionaries extracted by extract_locales.py and, for each
non-English language, writes game-locales/vocab/<code>.txt pairing the
English value with that language's value for a curated set of keys: the
game's own words for everything the mod's authored strings also name
(resources, contexts, actions, directions, combat verbs). Translators match
these so mod speech and game text stay one vocabulary.
"""
import sys
from pathlib import Path

# Curated: every key whose value names something an authored string also names.
KEYS = [
    # Resource nouns (ResourceHealth/MaxHealth/Food/Gold/IronOre/Tokens)
    "STAT_TITLE_HEALTH", "STAT_TITLE_FOOD", "STAT_TITLE_GOLD", "STAT_TITLE_IRONORE",
    "FORTUNE_CATEGORY_MAXHEALTH", "ENCOUNTER_CATEGORY_TOKEN",
    # Score/progress (ProgressScore, the level word)
    "SCORE_TITLE", "SCORE_LEVEL", "SCORE_DUNGEON",
    # Contexts the screen titles also name
    "ENCOUNTER_SHOP", "ENCOUNTER_CATEGORY_COMBAT", "ENCOUNTER_CATEGORY_EQUIPMENT",
    "MENU_PAUSED", "MENU_PAUSE_SETTINGS", "MENU_PAUSE_RESUME", "MENU_PAUSE_CONTROLS",
    "MENU_NAV_INVENTORY", "MENU_NAV_DECK_BUILDER_SHOW", "TREASURE_TITLE_MAPPEEK3",
    # Actions (ControlContinue, AddToDeck, shop wording)
    "MENU_NAV_CONTINUE", "MENU_NAV_BUY", "MENU_NAV_SELL", "MENU_NAV_ADD",
    "MENU_NAV_REMOVE", "MENU_NAV_BACK",
    # Status words (ShopInsufficient, DeckInsufficient, CardLocked/MapCellLocked)
    "SHOP_INSUFFICIENT_GOLD", "DECK_BUILDER_INSUFFICIENT", "UPGRADE_TITLE_LOCKED",
    # Directions (DirUp/Down/Left/Right) and combat verbs (glossary cue names)
    "KEY_CONTROL_UP", "KEY_CONTROL_DOWN", "KEY_CONTROL_LEFT", "KEY_CONTROL_RIGHT",
    "KEY_CONTROL_ATTACK", "KEY_CONTROL_COUNTER", "KEY_CONTROL_DODGE", "KEY_CONTROL_PAUSE",
]

TARGETS = ["fr", "it", "de", "es", "pt-br", "ru", "hu", "uk", "ja", "ko", "zh"]

def read_locale(path: Path) -> dict:
    table = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("//") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        table[key.strip()] = value.strip()
    return table

def main():
    locale_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent.parent / "game-locales"
    out_dir = locale_dir / "vocab"
    out_dir.mkdir(exist_ok=True)
    english = read_locale(locale_dir / "en.txt")
    missing = [k for k in KEYS if k not in english]
    if missing:
        sys.exit(f"keys missing from en locale: {missing}")
    for code in TARGETS:
        table = read_locale(locale_dir / f"{code}.txt")
        lines = []
        for key in KEYS:
            theirs = table.get(key, "<MISSING>")
            lines.append(f"{english[key]} => {theirs}    [{key}]")
        (out_dir / f"{code}.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"{code}: {sum(1 for k in KEYS if k in table)}/{len(KEYS)} keys")

if __name__ == "__main__":
    main()
