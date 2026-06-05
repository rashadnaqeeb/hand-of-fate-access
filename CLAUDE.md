# HandOfFateAccess - Claude Code Instructions

HandOfFateAccess is an accessibility mod that makes **Hand of Fate** (the original) playable by blind users. Speech is the sole interface; there is no visual fallback. Weigh every decision against this: if something fails silently, speaks stale data, or omits information, the player has no way to know. A logged failure is actionable; a silent one is invisible.

This is the author's fourth such mod. Two prior mods are the architectural references and live on this machine:
- `../oni-access` (Oxygen Not Included) — source of the **speech layer** and the **audio/sonification** subsystem.
- `say-the-spire2` (Slay the Spire 2, on GitHub, not local) — source of the **OOP focus-tapping design**.

## Target game / environment

- Game: Hand of Fate (Steam app 266510), at `C:\Program Files (x86)\Steam\steamapps\common\Hand of Fate`
- Engine: **Unity 5.3.7f1, Mono backend, 32-bit (x86)**
- Game code: `Hand of Fate_Data\Managed\Assembly-CSharp.dll`
- UI toolkit: **NGUI** (UICamera, UILabel, UIButton, UIPanel...). Gamepad input: **InControl** library.
- Loader: **BepInEx 5.x, x86 build** + Harmony. The mod assembly targets **.NET 3.5**.
- Decompiled game source for reference: `HoF-Decompiled/Assembly-CSharp/` (1813 files, read-only, not part of the build). **Look up any game type/method/field here before guessing at a signature.**
- Player log (appears when the game runs): `%USERPROFILE%\AppData\LocalLow\Defiant Development\Hand of Fate\output_log.txt`. Mod lines are prefixed `[HoFAccess]`.

## Architecture — three tiers

The build plan and status live in `ROADMAP.md`.

1. **UI (almost everything: dealer, cards, equipment, menus).** The game already has full controller navigation; we do NOT rebuild it. We Harmony-postfix **`UICamera.SetSelection(GameObject go, ControlScheme scheme)`** — the single static chokepoint that fires on every selection change — and announce the newly focused object. Read its text via `UILabel.text` on the object or its children. Most content work is per-control readouts (a focused card speaks name + full text + costs + tokens, not just its title) via a Proxy/UIElement model with a generic fallback proxy.
2. **Map (special).** A free-roam cursor (right stick / IJKL) over the whole VISIBLE board, independent of game focus, reading each tile and how it connects. Board is a 2D grid: `Map.Instance.MapLayout` exposes `GridSize`, `Slots`, `Nodes`, `GetSlot(Vector2)`, plus `SimplePathFinder PathFinder` and `PlayerCounter`. Each `MapLayoutSlot` has `GridPosition`, `Category`, `ActiveCard`, `IsUnlocked`, `IsComplete`, `IsNextTo(slot)`, `SimplePathNode`. Scope visibility to what a sighted player sees by mirroring `MapLayoutSlot.CheckDarken` / `IsUnlocked`.
3. **Combat (deferred, fully custom).** Real-time action that does not map to focus-reading. Core need is **audio**: sonify projectiles and attacks, tone-mark walls. Built on the audio engine lifted from oni-access (EarconScheduler, Sonifier, ShapeEarconPlayer, FollowMovementEarcon, ScannerDirectionEarcon, FootstepPlayer).

### Reusable spine (from oni-access `OniAccess/Speech/`)
`ISpeechBackend` + `TolkBackend` (P/Invoke Tolk.dll → NVDA/JAWS/SAPI) + `PrismBackend` (cross-platform native lib) + static `SpeechEngine` facade + `SpeechPipeline` (enable toggle, dedup window, interrupt-vs-queued) + `TextFilter` (markup stripping). Port to .NET 3.5. **Native libs must be x86 for this game** (Tolk ships x86; Prism would need a 32-bit build).

## Build & Deploy

BepInEx 5 plugin (`BaseUnityPlugin`, `[BepInPlugin]`). A `build.ps1` deploys the DLL + native deps into `<game>\BepInEx\plugins\`. (Created in ROADMAP Phase 0.) PowerShell is blocked from this agent by a deny rule — ask the user to run build/launch via the `! <command>` prompt prefix.

When a build fails on a type or method signature, look it up in `HoF-Decompiled/` before guessing.

## Harmony & reflection discipline

- Patch **non-virtual chokepoints**, never virtual base methods (overrides won't be intercepted). `UICamera.SetSelection` is the model: one static method, all selection flows through it.
- Patch class naming: `GameType_MethodName_Patch` (e.g. `UICamera_SetSelection_Patch`).
- Use a validated manual-patch helper that logs success/failure per patch; do not rely on blanket `PatchAll` that silently skips failures.
- Reflection into game internals uses `AccessTools.Field/Property/Method`. A crash on a renamed target is PREFERRED over silent degradation that misleads a blind user — so `!` / no-null-guard is intentional on game-internal reflection. For everything else (legitimately-nullable lookups) use `?.`, early returns, or `if (x is T t)`.
- Keep a running list of reflected private game members in this file (see Reflection Targets below) so game updates have one audit point.

## Focus & speech invariants

- Focus announcing happens ONCE per frame in an update loop. Hooks/setters only store state + set a dirty flag — never speak directly from a hook.
- All speech goes through `SpeechPipeline`; never call the backend directly.
- All logging goes through the mod `Log` helper; never `UnityEngine.Debug.Log` directly. `UnityEngine.Input` must be fully qualified inside any `*.Input` namespace.
- Never interrupt existing speech unless an action genuinely supersedes it (navigation). Default to queued.

## Never cache game state

Re-query the game every time you need a value. A blind player trusts speech absolutely; stale data is worse than none. The only acceptable "cache" is holding a reference to a live component (a `UILabel`, a `MapLayoutSlot`) and reading its properties at speech time. When multiple proxies/buffers read the same game model, centralize those reads in one View class so a game update touches one file.

## Reuse game data, avoid hardcoding

Prefer the game's own localized text and live UI state. Hardcoded strings go stale across updates and block translation. Only author mod strings when no game source exists.

## Accessibility announcement rules (mod-authored text only — never reword game text)

Users are experienced screen-reader users. Strip fluff, never strip information.
- Put the distinguishing word first ("anchored cursor", not "cursor anchored") so the user hears the difference immediately.
- No positional counts ("3 of 10") — the screen reader tracks list position.
- No navigation hints ("press Enter to select") except for unusual controls, and on a delay.
- No redundant context ("you are now in...") or obvious type suffixes.
- DO include all gameplay-relevant detail (card text, costs, tokens, tile connections). Concise means no fluff, not less information.
- Avoid emdashes and fancy punctuation; screen readers stumble on them.

## No silent failures

Every catch block logs via `Log.Warn`/`Log.Error`. Never an empty catch, never catch-and-return-default without logging. Harmony patches and reflection fail invisibly otherwise.

## LLM antipatterns to avoid

- Comments describe current state, not change history. (WRONG: "// now uses controller". RIGHT: "// closed via controller".)
- Don't over-null-check. Only guard where null is a legitimate expected state (after `FirstOrDefault()`, at public boundaries). Let it crash otherwise — a crash is visible, a swallowed null is not. Trust private callers.
- Don't pad or invent concerns. No issues means say "no issues."

## Edit discipline

Read the exact lines immediately before editing; never compose `old_string` from memory. Working-tree files are CRLF on Windows; match bytes exactly.

## Reflection Targets (audit after game updates)

Private game members accessed by reflection. (Populate as the mod grows.)
- `UICamera` — `SetSelection` (patch target), `selectedObject`, `currentScheme`, `mCurrentSelection`.

## Open decisions

- **Speech backend: Tolk (DECIDED 2026-06-05).** x86 `Tolk.dll` + companion DLLs are supplied by the user and deployed alongside the plugin. Only the `TolkBackend` is lifted from oni-access; `PrismBackend` is not used here.
- Exact tile-visibility rule for the map explorer (mirror `CheckDarken`).
- Combat interaction design (Phase 6).
- Project name `HandOfFateAccess` / plugin id — confirm before scaffolding.
