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
Split every focus readout into two layers so logic stays testable: a thin Unity **adapter** (in the plugin) that extracts raw state from the focused GameObject and does NO formatting, and **Core** composition that turns that raw state into the spoken string. The adapter is minimal and validated only in-game; the composition is unit-tested. Keep Unity types from leaking past the adapter boundary.
- [x] Stand up the validated manual Harmony-patch helper (logs success/failure per patch — see Cross-cutting) and use it for this and all later patches. (`Patching/HarmonyPatcher`.)
- [x] Harmony-postfix `UICamera.SetSelection`; on selection change in the Controller scheme, store the focused GameObject and set a dirty flag. Never speak from the hook. (`Patches/UICamera_SetSelection_Patch` -> `Focus/FocusTracker`; scheme-scoped to Controller, which NGUI also uses for keyboard arrow nav.)
- [x] Adapter: pull raw text from the focused object (`UILabel.text` on it or its children) into a plain DTO defined in Core (e.g. focused-control name plus its label texts). No Unity types in the DTO. (`Focus/FocusAdapter` -> `Core/Focus/FocusDto`.)
- [x] Core: compose the announcement from the DTO and unit-test the composition (empty labels, multiple child labels, markup via `TextFilter`). (`Core/Focus/FocusComposer`, `FocusComposerTests`.)
- [x] Announce once per frame from the update loop via the dirty flag; route through `SpeechPipeline`. (`Plugin.PumpFocus`, interrupt mode since navigation supersedes.)
- **Proof:** navigating the main menu with keyboard/controller speaks each focused item.
- **Architectural proof:** the focus-to-text composition is covered by offline tests; only raw extraction needs the game.

## Phase 3 - Proxy model + full readouts
- [x] `UIElement` base + `ProxyFactory` generic fallback (read all child labels, not just the first). (`Core/UI/UIElement.cs` with `GenericElement`; `Focus/ProxyFactory` is the single dispatch point, resolving the owning `Card` via `GetComponentInParent`.)
- [x] `Container` hierarchy + `FocusContext` path-diffing (announce only changed container context). **Realized one layer up at the screen level:** NGUI container/panel names are developer-internal, but mod-authored screen names are clean, so "announce only the changed context" now lives in `ScreenStack`'s entry-diffing (`Core/Screens/ScreenStack.cs`) keyed on the game's `GameState` machine rather than on nameless NGUI containers.
- [x] `Message` system (composable, markup-stripped at resolve). (`Core/UI/Message.cs`: ordered raw parts, filtered/empty-dropped/deduped/joined at `Resolve`.)
- [x] Targeted proxies for the high-value controls: encounter cards (name + full text + costs + tokens), equipment, dealer prompts. Use View classes for shared game-model reads. (One `CardElement`/`CardInfo` covers all `Card` subclasses - encounter, equipment, gold/food/health stat counters; encounter cards also read token stakes and completion. Dealer prompt is the encounter card's `Description`. Fame is not an in-run resource. No separate View needed since one card reader suffices; add View classes when a screen shares model reads across controls.)
- [x] `ScreenManager` (built as the `Screens` layer) — screen detection driven by the game's `GameState` machine, a Core `ScreenStack` with entry-announcement diffing, encounter/combat/shop overlays edge-detected from singletons, and the screen name spoken on entry. (`Core/Screens/{ScreenId,ScreenCatalog,ScreenStack}.cs`, `Screens/GameScreenWatcher.cs`.) Overlays that live outside the GameState machine are detected the same way: the pause menu (`PauseMenuManager` state) and modal dialogues (`MenuManager` Dialogue stack), the latter reading the live prompt text via `DialogueReader`/`DialogueInfo` so a modal is never silent. The stack is exposed for Phase 4 input dispatch and Phase 5 focus override to consume; the per-screen focus-resolution override and input-claim seams are defined but not wired (no dead dispatch). `ProxyFactory.Create` remains the focus resolver until Phase 5 needs the override.
- **Proof:** a focused card/equipment reads its complete information, correctly, across the main game screens; each screen names itself on entry and modal/pause overlays announce. *(Confirmed in-game; passed a multi-reviewer code pass whose findings - the queued-focus latch, nested dialogues, selection-forwarding focus, the gain/lose wording sitting in the adapter, and the catch re-touching a destroyed object - are fixed.)*

## Phase 4 - Input handler
The mod needs its own keys for things the game has no control for (map cursor, on-demand status lookups). This phase builds the input layer those depend on. (Review buffers and an F1 help overlay were considered and cut as unnecessary: the game's own controller navigation plus the focus readouts already cover review, and dedicated status keys serve the lookup need more directly.)
- [ ] `InputManager`: rebindable actions, keyboard + controller, falling back to the game's own action when a key is unclaimed so we never swallow game input.
- [ ] First consumer - map-explorer navigation keys (Phase 5): Ctrl+arrows on keyboard, right stick on controller (pending confirmation the game doesn't already use the right stick).
- [ ] Later - status keys that speak a specific value on demand (health, gold, food, etc.) without moving game focus. Deferred until those values exist as readable views; listed here so the input layer is designed to carry them.
- **Proof:** a bound mod key fires its action, and an unclaimed key still reaches the game's own handler.

## Phase 5 - Map explorer
- [ ] Determine how to capture the map-cursor input (right stick / Ctrl+arrows) without the game consuming it — via InControl's API or `UnityEngine.Input`, and check Steam Input isn't remapping the right stick out from under us.
- [ ] Grid cursor over `Map.Instance.MapLayout` via `GetSlot(Vector2)`; move with right stick / Ctrl+arrows.
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
- [x] Validated manual Harmony-patch helper (logs success/failure of each patch). (`Patching/HarmonyPatcher`.)
- [ ] Mod settings (speech rate/backend, verbosity, keybinds).
- [ ] `changes.md` changelog, player-perspective entries.
- [ ] Keep `CLAUDE.md` Reflection Targets list current.
