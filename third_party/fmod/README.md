# FMOD Core (vendored)

FMOD is the mod's non-speech audio backend (`HandOfFateAccess\Audio\FmodAudioBackend.cs`).
It replaced Unity's AudioSource pool because Unity's mixer denied three things this
audio-only mod needs: a flat hard stereo pan, low trigger latency, and freedom from
positional postprocessing applied behind our back. See that file's summary for detail.

The build requires this SDK; it is not committed (license-gated) and not optional. FMOD's
SDK is behind a mandatory account login, so it cannot be fetched automatically. These files
must be downloaded once by hand and dropped in here.

## What to download

1. Make a free account at https://www.fmod.com and sign in.
2. Download **FMOD Engine** for **Windows**, the **2.03** (or any 2.0x) version.
3. Run the installer (default location is fine).

## What to copy here

From the installed SDK (default `C:\Program Files (x86)\FMOD SoundSystem\FMOD Studio API Windows`).
This repo is vendored against **FMOD 2.03.20**.

- `api\core\lib\x86\fmod.dll`  ->  `third_party\fmod\lib\x86\fmod.dll`
  The 32-bit runtime. The game is x86, so the x86 dll is required (not x64). Use the release
  `fmod.dll`, not the logging `fmodL.dll`.
- The core C# binding, all three files, from `api\core\inc\`  ->  `third_party\fmod\binding\`:
  `fmod.cs`, `fmod_dsp.cs`, `fmod_errors.cs`. The csproj compiles these in when built with the
  FMOD backend, so no managed assembly reference is needed. `fmod.cs` depends on the DSP types
  in `fmod_dsp.cs`; `fmod_errors.cs` gives readable RESULT names. Using the SDK's own binding
  (rather than a hand-rolled one) keeps the P/Invoke struct layout matched to the dll version.

Do **not** copy the rest of the SDK (other headers, libs, docs, tools); only the runtime dll is
redistributable, and these three sources are all the build needs.

## net35 patch applied to the binding

The official binding targets .NET 4.5.1+, but the plugin must stay net35 for Unity 5.3's Mono.
Two adjustments make it compile on net35; both are already applied to the vendored copies and
must be reapplied if you re-vendor a newer SDK:

1. The binding uses the generic `Marshal` overloads added in 4.5.1. Rewrite each to its
   non-generic form (run from `third_party\fmod\binding\`):

       for f in fmod.cs fmod_dsp.cs fmod_errors.cs; do sed -i -E \
         -e 's/Marshal\.SizeOf<([A-Za-z0-9_.]+)>\(\)/Marshal.SizeOf(typeof(\1))/g' \
         -e 's/Marshal\.PtrToStructure<([A-Za-z0-9_.]+)>\(([A-Za-z0-9_.]+)\)/(\1)Marshal.PtrToStructure(\2, typeof(\1))/g' \
         -e 's/Marshal\.GetDelegateForFunctionPointer<([A-Za-z0-9_.]+)>\(([A-Za-z0-9_.]+)\)/(\1)Marshal.GetDelegateForFunctionPointer(\2, typeof(\1))/g' \
         "$f"; done

2. `fmod_dsp.cs` uses C# 8 `readonly` struct members, so the plugin's `LangVersion` is 8.0.
   That is set in the csproj, not in the binding, so re-vendoring needs no change for it.

## Build and run

    .\build.ps1

The build compiles in the binding and deploys `fmod.dll` beside the plugin. It fails with a
clear message if the SDK is not vendored here. `release.ps1` does the same and stages
`fmod.dll` into the player zip.

## Verifying the two-FMOD-systems question

The game's own engine (Unity 5.3 is FMOD internally) already runs one FMOD System; this
backend starts a second one in the same process. To confirm they coexist:

1. `.\build.ps1`, launch the game.
2. In `output_log.txt`, find the `[HoFAccess] fmod ... output ... dsp buffer ...` line. The
   output should be a real device (e.g. `WASAPI`), not `NOSOUND`. `NOSOUND` means our System
   failed to grab the device, i.e. a conflict.
3. Start a fight and move toward a wall. You should hear both the game's own audio (music,
   combat) and the mod's wall tones at the same time. Both playing together is the proof.

The default output is the shared OS mixer, which does not take the device exclusively, so they
are expected to coexist. If `NOSOUND` shows up or the game's audio cuts out, that is the
conflict to chase (try forcing `setOutput` to WASAPI, or check the driver).

## License obligation

FMOD is free for this non-commercial mod but requires a credit line. Keep `LICENSE.TXT` from
the SDK here and ensure the project's README/credits names "FMOD" and "Firelight
Technologies." See the chat discussion for the redistribution terms.
