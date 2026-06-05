# HandOfFateAccess - Claude Code Instructions
	 
HandOfFateAccess makes **Hand of Fate** (the original) playable by blind users. Speech is the sole interface; there is no visual fallback. If something fails silently, speaks stale data, or omits information, the player has no way to know. A logged failure is actionable; a silent one is invisible.

## Game & environment

- Engine: **Unity 5.3.7f1, Mono, 32-bit (x86)**. UI toolkit is **NGUI** (UICamera, UILabel, UIButton...); gamepad input is the **InControl** library.
- Loader: **BepInEx 5.x (x86)** + Harmony. The mod assembly targets **.NET 3.5**.
- Game code: `<game>\Hand of Fate_Data\Managed\Assembly-CSharp.dll`, where `<game>` is the Steam `steamapps/common/Hand of Fate` folder.
- Decompiled game source for reference: `HoF-Decompiled/` (gitignored). **Look up any game type/method/field signature here before guessing.**
- Player log: `%USERPROFILE%\AppData\LocalLow\Defiant Development\Hand of Fate\output_log.txt`. Mod lines are prefixed `[HoFAccess]`.

## Build, deploy, logs

- `build.ps1` builds the plugin and deploys the DLL plus native deps into `<game>\BepInEx\plugins\`.
- Run `build.ps1` directly via PowerShell (allowed by this project's `.claude/settings.local.json`). When invoking PowerShell through the Bash tool, escape `$` as `\$` so bash doesn't expand it first.
- Speech backend is **Tolk** (x86). 
- When a build fails on a signature, look it up in `HoF-Decompiled/` before guessing.

## Architecture

Phased build plan is in `ROADMAP.md`. Three tiers:

1. **UI (almost everything: dealer, cards, equipment, menus).** The game has full controller navigation; we do NOT rebuild it. Harmony-postfix **`UICamera.SetSelection(GameObject, ControlScheme)`** — the static chokepoint fired on every selection change — and announce the focused object, reading text via `UILabel.text` on it or its children. Per-control readouts (a card speaks name + full text + costs + tokens, not just its title) use a Proxy/UIElement model with a generic fallback proxy.
2. **Map.** A free-roam cursor (right stick / IJKL) over the whole VISIBLE board, independent of game focus. `Map.Instance.MapLayout` exposes `GridSize`, `Slots`, `Nodes`, `GetSlot(Vector2)`, plus `SimplePathFinder` and `PlayerCounter`. Each `MapLayoutSlot` has `GridPosition`, `Category`, `ActiveCard`, `IsUnlocked`, `IsComplete`, `IsNextTo(slot)`, `SimplePathNode`. Scope visibility by mirroring `MapLayoutSlot.CheckDarken` / `IsUnlocked`.
3. **Combat.** Real-time action; does not map to focus-reading. Built on **audio**: sonify projectiles/attacks, tone-mark walls (the oni-access audio engine).

## Conventions & invariants

**Harmony / reflection**
- Patch non-virtual chokepoints, never virtual base methods (overrides won't be intercepted).
- Patch class naming: `GameType_MethodName_Patch` (e.g. `UICamera_SetSelection_Patch`).
- Manual patching via a helper that logs success/failure per patch; never blanket `PatchAll` that silently skips failures.
- Game-internal reflection uses `AccessTools.Field/Property/Method`; a crash on a renamed member is PREFERRED over silent degradation, so no null-guard there is intentional. Use `?.` / early returns only for legitimately nullable lookups.

**Speech & focus**
- Announce focus ONCE per frame from an update loop; hooks/setters only store state and set a dirty flag — never speak from a hook.
- All speech goes through `SpeechPipeline`; never call the backend directly. All logging through the mod `Log` helper, never `Debug.Log`. Inside any `*.Input` namespace, fully qualify `UnityEngine.Input`.
- Never interrupt existing speech unless an action supersedes it (navigation). Default to queued.
- **Never cache game state.** Re-query every time; the only acceptable "cache" is a reference to a live component read at speech time. When several callers read the same game model, centralize reads in one View class.
- Prefer the game's own localized text and live UI state over hardcoded strings.

**Announcements (mod-authored text only — never reword game text).** Users are expert screen-reader users; strip fluff, never information.
- Distinguishing word first ("anchored cursor", not "cursor anchored").
- No positional counts ("3 of 10") — the reader tracks position. No nav hints unless an unusual control, and on a delay. No redundant context or obvious type suffixes.
- Include all gameplay-relevant detail (card text, costs, tokens, tile connections). Avoid emdashes and fancy punctuation.

**No silent failures.** Every catch logs via `Log.Warn`/`Log.Error`. No empty catches, no catch-and-return-default without logging.

**Testing.** Offline test project, no game launch. Prioritize silent-failure surfaces: `TextFilter` (full regression suite), `Message` composition, announcement formatting. Don't test thin proxies that just read a live component.

**Editing & style.** Read exact lines before editing; never compose `old_string` from memory (CRLF on Windows — match bytes). Comments describe current state, not change history. Don't over-null-check — let it crash where null isn't expected; a crash is visible, a swallowed null is not. Don't pad or invent concerns.

## Reflection targets (audit after game updates)

Private game members reached by reflection — one audit point for game updates.
- `UICamera` — `SetSelection` (patch target), `selectedObject`, `currentScheme`, `mCurrentSelection`.

## Common LLM Antipatterns

### Comments referring to what changed
Comments should describe the current state, not the change history. Consider whether a comment is needed at all.

**WRONG**: `// Removed the old UI system. Now x does y.`
**WRONG**: `// Changed to use controllers. Now handles force_close`
**CORRECT**: `// Can be closed with the controller`

### Defensive null handling
Excessive validation hides bugs. Only null-check where null is a legitimate, expected state (e.g., after `FirstOrDefault()`, at public API boundaries). Let code crash otherwise — a crash is visible, a silently swallowed null is not. Trust private callers.

**WRONG** — silently returning empty instead of crashing:
```csharp
if (entity == null) return new List();
var controller = entity.GetControlBehavior();
if (controller == null) return new List();
```

**WRONG** — `?.` on things that should never be null:
`var name = entity?.GetController()?.Sections?.FirstOrDefault()?.Name ?? "default";`

**CORRECT**: `var name = entity.GetController().Sections.FirstOrDefault()?.Name ?? "default";`
