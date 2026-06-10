# HandOfFateAccess - Claude Code Instructions
	 
HandOfFateAccess makes **Hand of Fate** (the original) playable by blind users. Speech is the sole interface; there is no visual fallback. If something fails silently, speaks stale data, or omits information, the player has no way to know. A logged failure is actionable; a silent one is invisible.

## Game & environment

- Engine: **Unity 5.3.7f1, Mono, 32-bit (x86)**. UI toolkit is **NGUI** (UICamera, UILabel, UIButton...); gamepad input is the **InControl** library.
- Loader: **BepInEx 5.x (x86)** + Harmony. The mod assembly targets **.NET 3.5**. The exact build (BepInEx 5.4.23.2 x86) is vendored in `third_party/bepinex/`; `setup-bepinex.ps1` installs it into the game folder.
- **Required BepInEx config (Unity 5.x):** in `<game>\BepInEx\config\BepInEx.cfg`, `[Preloader.Entrypoint]` must be `Type = MonoBehaviour` (default `Application` runs the chainloader during `Application..cctor`, which corrupts the first scene's NGUI deserialization: hundreds of "different serialization layout" / "missing script" errors and a hang on the loading screen before `card_table` loads). This file lives in the game folder, not the repo. `setup-bepinex.ps1` guarantees this (it patches an existing cfg or seeds one), so always (re)install BepInEx via that script rather than the raw upstream zip. Alternate entrypoint if needed: `Camera`.
- Game code: `<game>\Hand of Fate_Data\Managed\Assembly-CSharp.dll`, where `<game>` is the Steam `steamapps/common/Hand of Fate` folder.
- Decompiled game source for reference: `HoF-Decompiled/` (gitignored). **Look up any game type/method/field signature here before guessing.**
- Player log: `<game>\Hand of Fate_Data\output_log.txt` (this Unity 5.3 build writes it there, not under LocalLow). BepInEx also writes `<game>\BepInEx\LogOutput.log`. Both are truncated each launch. Mod lines are prefixed `[HoFAccess]`.
- Unity 5.3.7's old Mono runtime rejects `RegexOptions.Compiled` (throws `ArgumentOutOfRangeException` at `Regex` construction). Never pass it; same caution for other newer-runtime-only BCL features.
- Plugin `Awake` runs inside the BepInEx chainloader during `Application..cctor`, before the engine main loop starts. Touching most Unity APIs there hard-crashes the process (observed: `Time.unscaledTime` is fatal in `Awake`). Do only non-Unity setup in `Awake` (logging, native preload, P/Invoke); defer anything that reads Unity state, speaks, or reads focus to the `Update` loop. Note `SpeechPipeline.SpeakInterrupt` reads `Time` for its dedup window, so it too must not be called before the first `Update`.
- P/Invoke into Tolk is x86, so match the vendored binding exactly (`third_party/tolk/src/dotnet/Tolk.cs`): `CallingConvention.Cdecl`, every `bool` as `UnmanagedType.I1`, strings as `LPWStr`. Default 4-byte `bool` marshalling is tolerated on x64 but wrong on x86.

## Build, deploy, logs

- `setup-bepinex.ps1` installs the vendored BepInEx into the game folder with the correct entrypoint (run once per game install / after a game update wipes it). `build.ps1` builds the plugin (and Core transitively) and deploys both `HandOfFateAccess.dll` and `HandOfFateAccess.Core.dll` plus the x86 Tolk native deps into `<game>\BepInEx\plugins\`. `test.ps1` runs the offline test suite.
- Run `build.ps1` / `test.ps1` directly via PowerShell (allowed by this project's `.claude/settings.local.json`). When invoking PowerShell through the Bash tool, escape `$` as `\$` so bash doesn't expand it first.
- Shared MSBuild properties (`Version`, `LangVersion`) live in `Directory.Build.props` at the repo root; bump the version there.
- Speech backend is **Tolk** (x86). 
- When a build fails on a signature, look it up in `HoF-Decompiled/` before guessing.

## Architecture

Three projects (solution layout):
- **`HandOfFateAccess.Core`** — engine-agnostic logic (speech pipeline, `TextFilter`, `Log`, `IClock`). References **nothing** external (no Unity, no BepInEx). Multi-targets `net35` (consumed by the plugin) and `netstandard2.0` (consumed by tests). Keep it dependency-free so it stays unit-testable off-engine; put anything testable here.
- **`HandOfFateAccess`** — the BepInEx plugin (net35): engine/native glue only (`Plugin`, `TolkBackend`, `LogBepInExBackend`, `NativeLoader`). References Core. Build deploys both DLLs to `BepInEx/plugins`.
- **`HandOfFateAccess.Tests`** — net8 + xUnit, references Core only. Run with `test.ps1` (`dotnet test`). No Unity, no BepInEx, no game launch.

Engine seams are interfaces with injectable fakes: `ISpeechBackend` (real = `TolkBackend`), `IClock` (real = `StopwatchClock`, default; test = fake time). When new logic needs engine state, add a seam in Core and keep the Unity/BepInEx implementation in the plugin, rather than reaching into engine APIs from testable code.

**Adapter / composition split.** Reading live game state (focus, cards, map, tiles) inherently touches Unity and cannot live in Core. Keep that to a thin **adapter** in the plugin that only *extracts raw state* into a plain DTO (no Unity types past the boundary) and does **no formatting**. The announcement is then *composed* from the DTO by **Core** logic, which is unit-tested. The litmus test: if a piece of code decides what words the user hears, it belongs in Core; if it pulls a value off a `UILabel`/game object, it belongs in the adapter. This keeps test coverage growing with the mod instead of stranding announcement logic in the untestable plugin.

Phased build plan is in `ROADMAP.md`. Three tiers:

1. **UI (almost everything: dealer, cards, equipment, menus).** The game has full controller navigation; we do NOT rebuild it. Harmony-postfix **`UICamera.SetSelection(GameObject, ControlScheme)`** — the static chokepoint fired on every selection change — and announce the focused object, reading text via `UILabel.text` on it or its children. Per-control readouts (a card speaks name + full text + costs + tokens, not just its title) use a Proxy/UIElement model with a generic fallback proxy.
2. **Map.** A free-roam cursor (right stick / Ctrl+arrows) over the whole VISIBLE board, independent of game focus. `Map.Instance.MapLayout` exposes `GridSize`, `Slots`, `Nodes`, `GetSlot(Vector2)`, plus `SimplePathFinder` and `PlayerCounter`. Each `MapLayoutSlot` has `GridPosition`, `Category`, `ActiveCard`, `IsUnlocked`, `IsComplete`, `IsNextTo(slot)`, `SimplePathNode`. Scope visibility by mirroring `MapLayoutSlot.CheckDarken` / `IsUnlocked`.
3. **Combat.** Real-time action; does not map to focus-reading. Built on **audio** through the mod's own backend (Core procedural synthesis + an AudioSource voice pool: `Core/Audio` seam, plugin `UnityAudioBackend`; NOT lifted from oni-access). Live today: wall wind-tones, per-projectile voices, attack telegraph cues. Remaining: hazard voicing, the objects layer, the Dealer fight (see `ROADMAP.md` Phase 6).

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
- **Never cache game state.** Do not copy game data into mod-side dictionaries, lists, or string fields for later use; re-query the game when you need a value. A sighted player can see when the screen contradicts itself; a blind player trusts speech absolutely, so stale data is worse than no data. The only acceptable "cache" is holding a reference to a live Unity component (e.g. a `UILabel`) and reading its properties at speech time. When several callers read the same game model, centralize reads in one View class.
- **Reuse game data, avoid hardcoding.** Use the game's own localized text and live UI state wherever possible; look up game strings via `Localization.Localize(key)` / `UIUtils.GetString(key)`. Hardcoded text goes stale across game updates and blocks translation. Only author a string when no game data source exists (audited: screen names, "completed", and token gain/lose wording have no game-localized source; card names, descriptions, and costs are read live off `UILabel`).
- **No inline user-facing string literals.** Every word the mod itself authors and speaks must come from the mod's central strings file (a table in Core), never an inline literal, so the authored set can be translated later. Punctuation and log/debug text are exempt.

**Announcements (mod-authored text only — never reword game text).** Users are expert screen-reader users; strip fluff, never information.
- Distinguishing word first: the sooner the varying part appears, the faster the user moves on ("anchored cursor", not "cursor anchored").
- No positional counts ("3 of 10") — the reader tracks position. No nav hints unless an unusual control, and on a delay. No redundant context or obvious type suffixes.
- Include all gameplay-relevant detail (card text, costs, tokens, tile connections); concise means no fluff, not less information. Avoid emdashes (the reader announces them as "dash", breaking flow) and fancy punctuation.

**No silent failures.** The mod runs on Harmony patches and reflection, which fail invisibly unless logged: a swallowed exception in a patch silently stops a feature with no error the player can see. Every catch logs via `Log.Warn`/`Log.Error` what failed and where. No empty catches, no catch-and-return-default without logging. A logged failure is actionable; a silent one is invisible.

**Testing.** `HandOfFateAccess.Tests` (net8 + xUnit), runs via `test.ps1` — no game launch, no Unity. It references `HandOfFateAccess.Core` only, so anything you want covered must live in Core behind a seam, not in the plugin assembly. Prioritize silent-failure surfaces: `TextFilter` (full regression suite), `SpeechPipeline` (enable/dedup/filter), `Message` composition, announcement formatting. Don't test thin proxies that just read a live component. Static singletons (`SpeechPipeline`, `SpeechEngine`) mean tests reset shared state and parallelization is disabled assembly-wide.

**Editing & style.** Read exact lines before editing; never compose `old_string` from memory (CRLF on Windows — match bytes). Comments describe current state, not change history. Don't over-null-check — let it crash where null isn't expected; a crash is visible, a swallowed null is not. Don't pad or invent concerns.

## Reflection targets (audit after game updates)

Private game members reached by reflection — one audit point for game updates.
- `UICamera` — `SetSelection` (patch target), `selectedObject` (read at init to seed the first focus), `currentScheme`, `mCurrentSelection`.
- `UISelectable` — `Select(bool)` (patch target; the unified selection chokepoint reached by both navigation and programmatic/initial focus, which bypasses `UICamera.SetSelection`), `IsValid`, `IsSelectable`. `UISelection.SelectedObject`/`UISelectionManager` are the layered selection model behind it (Primary/Secondary/Dialogue categories). Also patched: `OnKey` (bool prefix; consumes the map cursor's Ctrl+arrows so the game's selection does not also move) and `DoClick` (prefix; marks the frame user-driven for the focus announce policy). `UISelection.m_selectionBlockerGroup` is read in `ProxyFactory` to mirror the game's blocked-selection check.
- `Game` — `Instance`, `ActiveGameState`, `OnGameStateEntered`/`OnGameStateExited` (the `GameScreenWatcher` subscribes to these instead of polling).
- `GameState_*` subclasses — the 17 concrete states mapped to `ScreenId` in `GameScreenWatcher.StateMap` via compile-time `typeof`, so a game-side rename breaks the build rather than mis-mapping. Audit the map after a game update.
- `Encounter.Instance` / `CombatEncounter.Instance` / `Shop.Instance` — sub-context overlays edge-polled from the Update pump.
- `MenuManager.Instance.PauseMenuManager.CurrentState.Name` (`PauseMenuManager.State.StateName`) — the pause overlay is a MenuManager push, not a GameState change, so it is edge-polled here. The game flips `ActiveGameState` to `GameState_Level_Pause` only when pausing from level play, so pause cannot be detected via the state machine alone.
- `MenuManager.Instance.CurrentMenu(MenuManager.StackPriority.Dialogue)` — modal dialogues (forfeit/quit confirms, error popups, the controller tutorial) are pushes on the Dialogue stack, also invisible to the GameState machine; edge-polled as the topmost overlay. `Dialogue` (`m_body`, `m_optionOKText`, `m_optionCancelText` UILabels, reached by reflection in `DialogueReader`) — the prompt/option text read into `DialogueInfo`.
- `CardSetModifierContainer` (the end-of-run reward screen) — `OnCardClicked(Card)` (patch target; flips a face-down reward card and moves focus on, so the reveal is announced from the pump via `RewardReveal`), `m_addSelectable` / `m_titleText` (the "add to deck" button and its banner, read in `ProxyFactory`). `Token` is resolved by `GetComponentInParent` in `ProxyFactory` for the reward-gem readout.
- Reward token naming (`TokenReader`) — a token has no localized name, so its granting card's title is found by scanning `CardManager.Instance.EncounterCards` and `CourtCardManager.Instance.CourtCardPrefabs` for an `EncounterCard` whose `TokenPrefabs` (or a sequence sub-encounter's, via `EncounterPrefab.SequenceEncounterCards`) has a matching `Token.Id`, then reading `Card.Title`. A renamed member crashes the readout (caught and logged in `BuildReadout`), which falls back to the id-synthesised name.
- `Scoreboard` (the end-of-run score breakdown) — `m_title` and `m_items` (each `ScoreboardItem`'s `m_title` / `m_score`), read by `ScoreboardReader`. The continue control is read structurally in `ProxyFactory` via `GetComponentInParent<Scoreboard>()`.
- Nav bar context actions (`NavBarReader`) — `UIManager.Instance.PrimaryNavBar` (a `MainNavBar`); the `Function0Button` / `Function1Button` (`NavBar` properties) and `NavBarButton.IsShowing` / `m_labels` are read for the Function0/Function1 actions (e.g. the scoreboard's high-score toggle and restart) that sit outside the focusable selection. Confirm/Cancel/Next/Prev/Inventory/Inspect are intentionally not read.
- Focus proxies (`ProxyFactory`) — `UIChoiceButton.m_choiceText` / `m_letterText` (encounter choice buttons), `CardTemplate.m_new` / `m_pinned` / `m_tokenSprite` (badge and token-gem state read off the template, which already folds sequence-encounter token logic), `MonsterCard.m_cardTitle` (the creature name alone; `LocalisedTitle` bakes in the count).
- Settings rows (`ProxyFactory`) — `VolumeSlider.m_slider` (the row's `UISlider`, its live 0..1 value spoken as a percentage); the per-type value labels `SubtitleToggle.m_label`, `UIProfileKeyDataToggle.m_valueLabel`, `UIGraphicsSettingsSelector.m_label`, `UIGraphicsSettingsResolution.m_resolutionLabel`, `UIGraphicsSettingsQuality.m_valueLabel`, `UIGraphicsSettingsFullScreen.m_valueLabel`, `UIGraphicsSettingsImageEffectToggle.m_valueLabel`, `LanguageVOSelect.m_languageLabel` (separated from the row title so a left/right change re-speaks only the value); key-binding rows `ControlBindElement.m_actionLabel` / `m_primaryLabel` (plus public `Invalid` for the conflict readout). The rebind flow is edge-polled off the public static `cInput.scanning` (Assembly-CSharp-firstpass) in `Plugin.PumpBindingScan`: the press-a-key prompt on scan start; on scan end `NavigationState.Mark()` (cInput ate the keypress) plus a delayed confirmation respeak when the binding did not change.
- Zoom/examine readers — `CardContainer.m_selectableContextTitle` / `m_selectableContextTitleParams` / `m_selectableContextText` / `m_cancelContextText` / `m_cardClickAction` / `m_cardCancelAction` (`ZoomReader`, the zoomed card's action prompts); `InfoPanel.m_title` / `m_statTitle` / `m_statValue` / `m_description` and `ComparePanelManager.m_old` (the equipment compare panel, also `ZoomReader`); `CabinetCardInfo.m_panelIndex` / `m_panels` (`CardInfoPanelReader`); `StartOptionsContainer.m_courtCardInfo` (`CabinetReader`).
- `UIEncounterEventPanel` — `m_encounterEventText` / `m_instructionsText` / `m_isShowing` (`EncounterEventReader`). The two labels populate in SEPARATE frames, so `EncounterNarration` speaks per-field deltas, never the recomposed pair. The public `RevealChancesCard` is read by the gambit's reveal detection.
- `UIDeathPanel.m_contextText` (`DeathPanelReader`); `Subtitle.m_label` / `m_transition` (`SubtitleReader`).
- `Credits.m_creditsLists` / `m_ksCreditsLists` (`CreditsReader`; `CreditsList.CreditsData` and the data's `Title`/`Entries` are public). The reset-progress pause panel has no component exposing its labels, so `GameScreenWatcher.PumpPauseState` reads the game strings by key (`MENU_SETTINGS_RESET_PROFILE`, `MENU_SETTINGS_RESET_PROFILE_PROMPT`); audit those keys after a game update.
- Gambit hooks (`.gambit` Harmony group, installed with the audio path, independent of speech) — `CardContainer.FlipCards(bool, bool)` (postfix; the chance cards flipping face-up triggers the Establish walk) and `CardChoiceContainer.AnimatedShuffle(int, float)` (postfix; an iterator method, so the postfix fires at coroutine creation = shuffle start). Live reads are public (`CardChoiceContainer.Cards`, `ChanceCard.ChanceType`).
- Combat hooks (`.combat` group, gated on `AudioEngine.IsAvailable`) — `CombatUtils.StartMeleeEffect` / `StartRangedEffect` (prefixes; the attack telegraph cue source, fired at parry-window open with the authoritative `a_blockable`), per-class `Begin` postfixes on the bespoke boss actions (each class verified to declare its own override; patching an inherited `Begin` would hit the base) plus `ActionHermitBomb.OnThrow`. Mover hazards: `CombatProxyLob.OnEngage` / `CombatProxyLightning.OnEngage` (postfixes on each class's own override; feed the flight voice and the per-attacker-gated launch cue), with the private `m_isExpiring` flag on both read each frame to stop a spent mover's voice. Cue records key their attacker via `GetComponentInParent<Targetable>` off the melee/ranged `Model` or the action's `ActorTransform`. In the main group: `Controller.OnFootstep` (bool prefix taking the private nested `Controller.Footstep` enum via `AccessTools.Inner`; suppresses wall-blocked footsteps).
- Combat diagnostics (`.diagnostics` group, installed even when audio is down) — `Destroyable.ApplyDamage` (prefix; logs every player hit with its source attack and seconds since the last cue) and `CombatProxy.Engage` / `Disengage` (postfixes; non-projectile hazard spawn recon, including each ground area's authored tuning fields).
- `CombatProxyArea` — `AllAreas` (public static registry, polled per frame by `ZoneSonification`; no patch); private `m_time` / `m_activationDelay` / `m_innerRadius` / `m_isProximityMine` / `m_isExpiring` read per frame for the zone voice, plus `m_growTime` / `m_timeOut` / `m_angle` dumped once per spawn by the engage recon log.

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
