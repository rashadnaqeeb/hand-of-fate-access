# HandOfFateAccess - Build Roadmap

Phased plan. Each phase ends with a concrete, verifiable proof in the real game before moving on. Check items off as completed. See `CLAUDE.md` for architecture and conventions.

## Phase 1 - Plugin, logging, and speech (prove the whole output chain)
Stand up the full speech and logging stack in one go, so the first run proves output works end to end, not just that a DLL loaded. There's no value in a bare loader milestone on its own.
- [x] Install BepInEx 5.x **x86** into the game folder; launch once to generate `BepInEx/` config. (Installed BepInEx 5.4.23.2 x86; config generates on the first launch.)
- [x] Create the plugin project (`HandOfFateAccess.csproj`), target **.NET 3.5**, reference BepInEx, 0Harmony, and the game's `Assembly-CSharp.dll` / `UnityEngine.dll` / NGUI types. `[BepInPlugin]` `BaseUnityPlugin` entry.
- [x] `build.ps1`: build + copy the plugin DLL and the x86 Tolk DLLs (+ companions) into `<game>\BepInEx\plugins\`.
- [x] Logging setup: a mod `Log` helper writing to the BepInEx logger / Unity log, every line prefixed `[HoFAccess]`.
- [x] Speech: lift `Speech/` from oni-access (`ISpeechBackend`, `TolkBackend`, `SpeechEngine`, `SpeechPipeline`, `TextFilter`); port to .NET 3.5. (`TextFilter` reworked for NGUI bracket markup.)
- [x] Pre-load `Tolk.dll` (companions co-located) at `Awake` so the `DllImport` resolves; log clearly if it doesn't.
- [x] In `Awake`: initialize logging and speech, log `[HoFAccess] loaded`, and speak a startup line.
- [x] **Proof:** launch the game and get BOTH — a `[HoFAccess]` line in `output_log.txt` / BepInEx console AND a spoken startup line through the screen reader. Full output chain working end to end, with logging and speech both in place. (Verified: log chain present, "Hand of Fate Access loaded" spoken via JAWS, `card_table` scene loads clean with zero serialization errors.)

## Phase 2 - Focus spine (prove the core bet)
- [ ] Harmony-postfix `UICamera.SetSelection`; on selection change in Controller scheme, read the focused GameObject's `UILabel` text.
- [ ] Route through `SpeechPipeline`. Announce once per frame via an update loop + dirty flag (never from the hook).
- **Proof:** navigating the main menu with keyboard/controller speaks each focused item.

## Phase 3 - Proxy model + full readouts
- [ ] `UIElement` base + `ProxyFactory` generic fallback (read all child labels, not just the first).
- [ ] `Container` hierarchy + `FocusContext` path-diffing (announce only changed container context).
- [ ] `Message` system (composable, markup-stripped at resolve).
- [ ] Targeted proxies for the high-value controls: encounter cards (name + full text + costs + tokens), equipment, fame/tokens, dealer prompts. Use View classes for shared game-model reads.
- [ ] `ScreenManager` + per-screen registries where a screen needs context the generic proxy can't give.
- **Proof:** a focused card/equipment reads its complete information, correctly, across the main game screens.

## Phase 4 - Input, review buffers, help
- [ ] `InputManager`: rebindable actions, keyboard + controller, dispatched through the screen stack, falling back to the game's own action when unclaimed.
- [ ] Buffer system (review cursor): step through detailed info (deck, equipment, current encounter) with Ctrl+arrows, without moving game focus.
- [ ] F1 help overlay built from the active screen stack.
- **Proof:** review buffers and rebindable keys work; F1 lists context controls.

## Phase 5 - Map explorer
- [ ] Determine how to capture the map-cursor input (right stick / IJKL) without the game consuming it — via InControl's API or `UnityEngine.Input`, and check Steam Input isn't remapping the right stick out from under us.
- [ ] Grid cursor over `Map.Instance.MapLayout` via `GetSlot(Vector2)`; move with right stick / IJKL.
- [ ] Read each slot: category, active card, lock/complete state, neighbors (`IsNextTo`), reachability (`SimplePathFinder`), distance/relation to `PlayerCounter`.
- [ ] Scope to visible tiles by mirroring `CheckDarken` / `IsUnlocked`.
- **Proof:** a blind player can explore the whole visible board and understand its layout and connections.

## Phase 6 - Combat (deferred design)
- [ ] Lift the audio/sonification subsystem from oni-access.
- [ ] Design combat interaction (sonify projectiles/attacks, tone-mark walls, enemy telegraphs, player position/facing).
- [ ] Implement and iterate.
- **Proof:** a representative fight is winnable without sight.

## Cross-cutting (ongoing)
- [x] Offline test project (no game launch). `HandOfFateAccess.Tests` (net8 + xUnit) over the dependency-free `HandOfFateAccess.Core`; run via `test.ps1`. Covers `TextFilter` (regression suite), `SpeechPipeline` (enable/dedup/filter), `SpeechEngine`. Extend with `Message` composition and announcement formatting as those land.
- [ ] Validated manual Harmony-patch helper (logs success/failure of each patch). Add when Phase 2 starts patching.
- [ ] Mod settings (speech rate/backend, verbosity, keybinds).
- [ ] `changes.md` changelog, player-perspective entries.
- [ ] Keep `CLAUDE.md` Reflection Targets list current.
