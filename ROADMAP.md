# HandOfFateAccess - Build Roadmap

Phased plan. Each phase ends with a concrete, verifiable proof in the real game before moving on. Check items off as completed. See `CLAUDE.md` for architecture and conventions.

## Phase 0 - Scaffold (prove the loader)
- [ ] Install BepInEx 5.x **x86** into the game folder; launch once to generate `BepInEx/` config.
- [ ] Create the plugin project (`HandOfFateAccess.csproj`), target **.NET 3.5**, reference BepInEx, 0Harmony, and the game's `Assembly-CSharp.dll` / `UnityEngine.dll` / NGUI types.
- [ ] Minimal `[BepInPlugin]` `BaseUnityPlugin` that logs `[HoFAccess] loaded` in `Awake`.
- [ ] `build.ps1`: build + copy DLL into `<game>\BepInEx\plugins\`.
- **Proof:** launch the game, see `[HoFAccess] loaded` in `output_log.txt` / BepInEx console.

## Phase 1 - Speech (prove output)
- [x] Speech backend decided: **Tolk** (x86). User supplies `Tolk.dll` + companion DLLs.
- [ ] Lift `Speech/` from oni-access (`ISpeechBackend`, `TolkBackend`, `SpeechEngine`, `SpeechPipeline`, `TextFilter`); port to .NET 3.5; deploy the x86 Tolk DLLs alongside the plugin.
- [ ] Initialize speech in plugin `Awake`; speak a startup line.
- **Proof:** the mod speaks through NVDA (or chosen backend) when the game starts.

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
- [ ] Logging discipline + a validated manual Harmony-patch helper (logs each patch).
- [ ] Mod settings (speech rate/backend, verbosity, keybinds).
- [ ] `changes.md` changelog, player-perspective entries.
- [ ] Keep `CLAUDE.md` Reflection Targets list current.
