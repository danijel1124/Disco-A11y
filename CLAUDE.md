# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a **Disco Elysium Accessibility Mod** that adds screen reader support and keyboard navigation to the game. The mod uses **MelonLoader** (a Unity game modding framework) and **Harmony** (for runtime patching) to inject accessibility features into the game.

Key technologies:
- **MelonLoader**: Unity modding framework for .NET 6.0
- **Il2Cpp interop**: Required for Unity IL2CPP builds
- **Tolk**: Screen reader integration library
- **Harmony**: Runtime method patching for game hooks

## Screen Takeover & TTS Announcement Protocol (hard rule)

The person testing this mod is **blind and works with a screen reader**. This changes how an AI assistant is allowed to drive the machine.

**Prefer approaches that never need window focus.** This repo ships the **AI Dev Bridge** (see below) precisely so tests drive the game and the mod through the game's own systems — no keystrokes, no foreground window. Taking over a blind user's screen is the single most disruptive thing a test can do. Use the bridge (`state`, `objects`, `navigate`, `interact`, `goto`, `view`, …) instead of GUI automation whenever possible.

**Never do GUI automation (SendKeys, SetForegroundWindow, focusing any window) while the user is at the machine.** Synthetic keystrokes land in the game, not your terminal, and silently disrupt what the user is doing. If the user is present, either wait until they step away, or don't automate the GUI at all — use the bridge. When in doubt whether they are present, ask first.

**If a takeover is genuinely unavoidable, follow this protocol without exception:**

1. **Announce first, via TTS**, that you are taking the window and that the user must not press anything.
2. **Narrate every single action via TTS while you hold the window.** Once the game window has focus, the terminal is invisible to the user — your text output does not exist for them. Silence reads as a crash or a stall, and they will take the window back and collide with you.
3. **Announce via TTS when you are done**, so the user knows they can work again. No "done" message means they assume you died and take over.

**TTS mechanics (what actually works):** speak through **SAPI inline, backgrounded** (run the command in the background), each utterance its own command:
```
Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak("...")
```
What does **not** work: `Start-Job` (the job dies with the short-lived shell — the speech silently never happens); `Start-Process powershell -WindowStyle Hidden` (does not reach the user). **Do not announce through NVDA** here: the mod itself speaks through NVDA/Tolk, so your announcements would collide with the game's own speech. SAPI is a separate voice and stays out of the screen reader's way.

## Build Commands

### Environment Setup
The mod requires `DISCO_ELYSIUM_PATH` environment variable pointing to the game installation:
```bash
export DISCO_ELYSIUM_PATH="/mnt/c/Program Files (x86)/Steam/steamapps/common/Disco Elysium"
```

### Build Commands
```bash
# Build and auto-copy to game (if DISCO_ELYSIUM_PATH is set)
./mod/build.sh

# Manual build
cd mod && dotnet build AccessibilityMod.csproj --configuration Release

# Test the build
dotnet build mod/AccessibilityMod.csproj --configuration Release
```

The build automatically copies the resulting DLL to `$DISCO_ELYSIUM_PATH/Mods/` if the environment variable is set.

## Architecture Overview

The mod follows a **modular system architecture** with clear separation of concerns:

### Core Systems (mod/ directory)
- **AccessibilityMod.cs**: Main entry point, initializes all subsystems and handles MelonLoader lifecycle
- **TolkScreenReader.cs**: Screen reader integration (NVDA, JAWS, SAPI fallback)

### Navigation System (mod/Navigation/)
- **SmartNavigationSystem.cs**: Central navigation coordinator with categorized object selection
- **NavigationStateManager.cs**: Manages current selection and object categories  
- **MovementController.cs**: Automated pathfinding and player movement
- **ObjectCategorizer.cs**: Classifies game objects (NPCs, locations, containers, etc.)

### Input Handling (mod/Input/)
- **InputManager.cs**: Centralized keyboard input processing with accessibility hotkeys - dispatches via `KeyBindings.IsPressed(GameKey.X)` rather than hardcoded `UnityEngine.Input.GetKeyDown(KeyCode...)` calls

### Settings (mod/Settings/)
- **GameKey.cs / KeyBindings.cs**: Every hotkey is a remappable `GameKey`, persisted via MelonPreferences to `UserData/AccessibilityMod.cfg` (`[KeyBindings]` category, one `"UnityKeyCodeName|RequireCtrl|RequireAlt|RequireShift"` string per action). Three built-in presets: the original US-QWERTY punctuation bindings (`Defaults` - punctuation keys like `[`, `]`, `\` move or require AltGr on non-US layouts such as German QWERTZ, which made the mod effectively unusable there), plus two layout-independent presets built around free F-keys after checking the game's own live keyboard bindings (Keypad1-9 pick dialogue options during conversations, Escape opens the pause menu, KeypadMinus zooms - see `GameKeybindConflictChecker`): `NumpadSafePreset` (F-keys + the two collision-free numpad keys Keypad0/KeypadDivide) and `StardewPreset` (identical but zero numpad usage - rule: must work on keyboards without a numpad). Both keep stardew-access's PageUp/PageDown/Ctrl+Home conventions; StopMovement is Space, the game's own stop key.
- **AccessibilityPreferences.cs**: Dialog reading mode / orb announcements / orb volume-voice-rate / repeat-suppression window / speech interrupt / speak sound captions / dialog auto-advance, same cfg file, `[AccessibilityMod]` category.

### UI Integration (mod/UI/)
- **UINavigationHandler.cs**: Detects and announces UI element selection
- **UIElementFormatter.cs**: Formats UI elements for screen reader output
- **DialogStateManager.cs**: Manages dialog reading modes

### Game Integration (mod/Patches/)
- **InteractableSelectionPatches.cs**: Harmony patches for game's interaction system
- **DialogSystemPatches.cs**: Patches for dialog system accessibility
- **OrbTextVocalizationPatches.cs**: Patches for skill check announcements
- **NotificationVocalizationPatches.cs**: Patches for game notifications
- **InventoryPatches.cs**: Patches for inventory accessibility

### Inventory System (mod/Inventory/)
- **InventoryNavigationHandler.cs**: Keyboard navigation for inventory screens

### Utilities (mod/Utils/)
- **GameObjectUtils.cs**: Game object discovery and player position detection
- **ObjectNameCleaner.cs**: Cleans up technical object names for user-friendly announcements
- **DirectionCalculator.cs**: Spatial navigation utilities

## Key Accessibility Features

### Navigation Hotkeys
All hotkeys are remappable (see Settings above); this lists the two built-in presets.

Default (original, US-QWERTY):
- **[** - Select NPCs category
- **]** - Select locations category  
- **\\** - Select containers/loot category
- **=** - Select everything category
- **.** - Cycle within current category
- **,** - Navigate to selected object
- **/** - Stop automated movement
- **;** - Full scene object scan
- **'** - Distance-based scene scan
- **`** - Announce current UI selection
- **-** - Toggle dialog reading mode

Layout-safe presets (recommended for non-US keyboards). Shared keys:
- **F2/F3/F4** - Select NPCs/locations/loot category (Ctrl+F2 focus waypoints, Alt+F2 create, Alt+F3 delete)
- **F6/F7/F8** - Announce current selection / toggle sorting mode / distance scan
- **Space** - Stop automated movement (same key the game itself uses)
- **Page Down / Page Up** - Cycle objects forward/backward
- **Ctrl+Page Down / Ctrl+Page Up** - Next/previous category (stardew-access convention; NPCs, Locations, Loot, Everything with wrap-around)
- **Ctrl+Home** - Navigate to selected object
- **F** - Interact with the selected object (keyboard equivalent of clicking it; the game's own E-Interact only works with controller selection)
- **F11** - Toggle speech interrupt, **Ctrl+B** - toggle diagnostics
- **G** - Toggle dialog auto-advance ("autoread": UI/DialogAutoAdvance.cs presses the game's own SunshineContinueButton once the screen reader finishes the current line; only active in full-text dialog reading mode, pauses automatically while response options or checks are up; same key in all presets)

Differing keys:
- With numpad (`NumpadSafePreset`): **Numpad 0** select everything, **Numpad /** toggle dialog reading
- Without numpad (`StardewPreset`): **F10** select everything, **F12** toggle dialog reading

### AI Dev Bridge (tools/DevBridge)
Development-only companion MelonMod (never in the mod release zip; ships in the tools bundle, installed via the installer's "Enable AI dev bridge" option / `--devbridge`, removed via `--no-devbridge`). Remote control so an AI assistant or script can drive the game and the accessibility mod without keyboard/screen access. **Two transports, same command set:**
- **Socket (preferred)**: TCP on `127.0.0.1`, port written to `UserData/DevBridge/port.txt` (48610, or an OS-assigned one if taken). One command per line; the response ends with a `<<END>>` line. Commands execute on the next frame (one-frame latency instead of up to 200 ms), and the socket also **pushes events** unprompted, each on a line starting with `! `: `! spoken <text>` (everything the screen reader says, live), `! dialog active` / `! dialog inactive`, `! scene <name>`. Accept/read run on background threads; commands and all writes happen on the Unity main thread (never touch Il2Cpp objects off-thread). `tools/DevBridge/bridge-client.ps1` is a minimal test client (`.\bridge-client.ps1 state`, `.\bridge-client.ps1 -Listen 30` to just stream events) - the protocol is plain line-based TCP, so any language can talk to it.
- **File (fallback)**: write one command line to `UserData/DevBridge/command.txt`, the bridge polls at 5 Hz, executes, and writes the result to `response.txt`.

Commands (send `help`): `state`, `objects [n]`, `spoken [n]` (speech history via Harmony postfix on `TolkScreenReader.Speak`), `screenshot [file]`, `select <cat>`, `cycle [back]`, `category next|prev`, `navigate`, `interact`, `stop`, `announce`, `dialog`, `continue`, `teleport x y z`, `readingmode`, `set autoread|autointeract|captions on|off`. Drives the main mod through `AccessibilityMod.NavigationSystem` (public static accessor) rather than simulating keys. Build: `cd tools/DevBridge && dotnet build -c Release` (auto-copies to Mods/).

**Driving the game itself** — all through the game's own systems, so none of them need a key press or the window focus (taking over a blind user's screen is the most disruptive thing a test can do):
- `scenes` / `goto <scene> <marker>` / `destinations` / `travel <id>`: move between areas via `AreaSpecificLuaFunctions.TeleportTo` and `TravelDestination.ArriveAt` — the real transition (loading screen, spawn point, camera), not a position hack. **The game silently ignores an area change while a dialogue is running**; wait for it to end.
- `view [type]`: open any screen (INVENTORY, JOURNAL, CHARACTERSHEET, SAVE, LOAD, OPTIONS, …) through `ViewController.GetViewByType` + `SwitchToView`. `view` with no argument prints the current view and the list.
- `save [name]` / `saves` / `quickload` / `loadnewest` (via `SunshinePersistence`): the save screen's slots are **not** EventSystem-navigable, so the `ui` commands cannot reach them — `save` calls `SaveWithScreenshot` directly. `loadnewest`/`quickload` refuse when no save exists (loading nothing hangs the game on a loading screen forever). A `martinaise-test` save parked outside in Martinaise means tests never replay the intro.
- `devmode`: the game's own `DebugModes.SetDeveloperMode`.

Note that the PC build has **no map screen**: `Il2CppPages.Gameplay.Map.MapPage` belongs to the `DiscoPages` system, which is dead code on PC (`FindObjectsOfType<DiscoPage>` returns 0 even with a screen open — PC uses `Sunshine.Views`), and `MapManager` belongs to the collage photo mode. The map's role is played by the in-world **quicktravel markers**, which `mod/Patches/MapPatches.cs` already covers.

### Mod Configurator Tool (tools/KeybindEditor)
A standalone WinForms app (not part of the mod DLL) for editing `UserData/AccessibilityMod.cfg` without launching the game: pick a game folder, load a preset or freely rebind any action to any key, edit all general settings (dialog reading mode, orb announcements, orb volume, orb voice, the repeat-suppression window, speech interrupt, sound captions, autoread, auto-interact), save. The **repeat-suppression** field (`RepeatSuppressionMinutes`) is a spin box in minutes (0 = off) - a screen reader announces the exact number as you arrow, and it is the one dial governing both looping orb lines and re-entered area descriptions. The **orb voice** dropdown (`OrbVoice`) is filled by asking the TTS server itself (`DiscoElysiumTtsServer --voices` via `InstalledVoices.cs`) rather than enumerating voices here - one source of truth, and it keeps the 25 MB Windows projection out of this small editor; "(System default)" stores an empty value. A **"Test voice"** button next to the dropdown plays a sample with the currently selected voice and volume before saving (`VoiceTester.cs` → the server's `--say` mode), so the unsaved choice can be heard. Started as a pure keybind editor, renamed to **DiscoElysiumModConfigurator.exe** once it covered every mod setting (project folder still `tools/KeybindEditor`). Localized (English/German). Optionally takes the game folder as `argv[0]` to skip the manual Browse step - used by the Start Menu shortcut the Installer creates.

Also has a non-interactive CLI mode for config maintenance after mod updates: `DiscoElysiumKeybindEditor.exe --cli [gamePath] [--preset default|numpad|stardew] [--list]`. Without `--preset` it syncs: actions the mod gained since the config was written are added with their default binding, everything else is left untouched (and the added actions are named in the output). With `--preset` all bindings are replaced by that preset (general settings kept). `--list` prints the resulting bindings. Attaches to the caller's console for output. Requires the mod to have been built and installed at least once so its `GameKey` list conceptually matches (the tool's `GameKeyCatalog.cs` is a hand-kept mirror of `mod/Settings/KeyBindings.cs`'s `Defaults`/`NumpadSafePreset` - keep both in sync when adding/renaming a `GameKey`). Build: `cd tools/KeybindEditor && dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`.

### Mod Debugger (tools/ModDebugger)
A WinForms window outside the game (`DiscoElysiumModDebugger.exe`), opened from inside the game with **Ctrl+Y** (`GameKey.OpenModDebugger`, gated on debug mode; `mod/Utils/ModDebuggerLauncher.cs` starts it and hands over the game folder). It loads `UserData/SpeechLog.txt`, connects to the **dev bridge socket** and shows every line the mod speaks as it is spoken, lets a comment be pinned to any single line (Enter/F2) and exports an annotated report to `UserData/ModDebugger-Report-*.txt`. Also has a bridge command box. Purpose: a bug report from a blind player no longer has to be written from memory. Accessibility constraints it is built around: single column (screen readers read lines, not grids), full keyboard operation, and live-follow disables itself the moment the player selects a line by hand — a list that keeps jumping to the newest entry cannot be read while the game keeps talking. Ships **in the mod release zip** (see delivery rule below). Build: `cd tools/ModDebugger && dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`.

### Orb TTS Server (tools/TtsServer)
The voice the **orb text** speaks with (`DiscoElysiumTtsServer.exe`), a short-lived child process the mod starts and feeds. The mod (`mod/OrbSpeech.cs`) writes it one JSON request per line on stdin - `{"speaker":"...","text":"..."}` - and knows nothing else: not the voice, the engine, the volume, or whether a line made a sound. The server owns all of that. It reads voice/volume/rate straight from `[AccessibilityMod]` in `AccessibilityMod.cfg` (`OrbVoice`, `OrbVolume`, `OrbRate` - re-read only when the file changes, so the configurator's changes land on the next line without a restart), synthesizes each line through WinRT `Windows.Media.SpeechSynthesis`, and plays it on the OS mixer via `MediaPlayer`, on a single thread so ambient orb lines queue instead of overlapping.

WinRT is the whole point: it is the only way to reach the modern voices - the OneCore voices (Hedda, Katja, Stefan) and, once the user installs them via Windows Settings → Accessibility → Narrator → natural voices, the natural/neural voices. Classic SAPI (the orb's old engine) cannot render either. The backend is behind `ITtsBackend` so an ElevenLabs or pre-rendered-WAV-per-speaker backend can slot in later without the mod changing a line - the `speaker` is carried through for exactly that (per-speaker voices). Lifecycle is tied to the game, not a clock: when the game exits the pipe closes, stdin hits end-of-stream, and the server drains and exits. Ships **in the mod release zip** as its own folder `Mods/TtsServer/` (it carries the ~25 MB WinRT projection and a few DLLs, so it is a folder, not a lone exe). net6 like everything else the mod ships: the mod is pinned to net6 by MelonLoader regardless, so keeping the server (and the tools) on net6 means the machine needs exactly one runtime, the .NET 6 Desktop Runtime, instead of two. The WinRT projection works fine on `net6.0-windows10.0.19041.0`. Diagnostic/utility modes: `--voices` (list installed voices, used by the configurator), `--render <wav> <text>` (synthesize without playing), and `--say <volume> <rate> <voice> <text>` (play one line with those exact settings, ignoring the config file - the configurator's "test voice" button uses it to preview an unsaved selection). Playback is identical to a real orb line; only the source of the settings differs (argv vs. the cfg file), which is why there is no separate "test" playback path. Build: `cd tools/TtsServer && dotnet publish -c Release --self-contained false`.

### Installer Tool (tools/Installer)
A standalone WinForms app that installs MelonLoader (dynamic latest-release lookup + PE-header architecture detection, pinned `v0.7.3` fallback) and the mod itself (from a GitHub release - defaults to `danijel1124/Disco-A11y` since upstream `game-a11y/Disco-A11y` has never published one) into an auto-detected or manually chosen game folder. By default it installs the latest stable release (`/releases/latest`, which ignores prereleases); the "Install latest prerelease (nightly)" checkbox / `--prerelease` CLI flag walks the full release list instead and takes the newest non-draft entry - that's the moving `nightly` prerelease channel (release policy: exactly one in-place-updated nightly for untested work, versioned releases only after real play-testing). Refuses to install while `disco.exe` is running (loaded DLLs are locked). Also has a permanent non-interactive CLI mode: `DiscoElysiumInstaller.exe --cli [gamePath] [--force] [--prerelease]` (skips reinstalling an already-present MelonLoader unless `--force`). Also localized (English/German), also vendors `TolkNative/` for spoken status. Build: same pattern as KeybindEditor, `cd tools/Installer && dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`.

**Delivery rule (hard): one path per kind of thing.** Anything that belongs to the mod ships in the **mod release zip** and is copied into the game folder by `ModInstaller.InstallFiles` — that includes `DiscoElysiumModDebugger.exe`, which is part of the mod (it only wakes on Ctrl+Y with debug mode on), and the `Mods/TtsServer/` folder, the orb voice the mod speaks with. Do not embed mod files in the installer binary and do not give them an install step of their own: a second channel is a second thing that can silently fall out of step with the release. Only what the *installer itself* needs to function is embedded — the Tolk natives, because an installer that has to download its own voice first is mute to a blind user until the network answers. The zip may wrap its files in a top-level folder or not; `FindExtractedRoot` keys off the presence of `Mods`, and a package without `Mods/AccessibilityMod.dll` is rejected rather than "installed" (it once copied nothing, wrote the version marker anyway, and told the player the new build was in).

**The setup exe never unpacks executables out of itself** (`ToolsBundle.cs`): the configurator and the dev bridge are downloaded from the release's tools zip at install time, like the mod and MelonLoader. It embeds only the Tolk natives — what it *speaks* with, not what it installs. This is not just tidiness: the earlier build, which carried those exes as resources and dropped them on startup, was quarantined on a user's machine as `Trojan:Win32/Bearfoos.B!ml` — a self-extracting, unsigned binary that drops executables, downloads more, and replaces itself is behaviourally indistinguishable from a dropper, and the ML heuristics said so. Keep it that way: no embedded executables, no self-extraction.

**The self-contained ("standalone") setup is retired.** It existed only to carry a .NET runtime; users install the **.NET 6 Desktop Runtime** once instead (`https://dotnet.microsoft.com/download/dotnet/6.0`, x64) - the installer downloads and runs it silently (`DotNetRuntime.cs`), and it is the one runtime that covers the mod, the tools and the TTS server. One setup asset (`DiscoElysiumSetup.exe`) remains, and an old standalone binary self-updates into it after being told it needs the runtime (`SelfUpdater.IsRetiredStandalone`).

After a successful install (GUI or CLI), it also creates a Start Menu shortcut (`StartMenuShortcut.cs`, via late-bound `WScript.Shell` COM so no COM reference is needed) to the Keybind Editor, in a subfolder under `%APPDATA%\Microsoft\Windows\Start Menu\Programs\` named after the game's install folder (matching wherever Steam itself would put per-game shortcuts) - localized shortcut name/description, pre-filled with the game path as an argument so opening it never requires Browse. Silently skipped (logged, not an error) if `DiscoElysiumKeybindEditor.exe` isn't found next to the installer (`KeybindEditorLocator.cs` - same lookup `MainForm`'s "Open Keybind Editor" button uses, so ship both exes side by side).

### Object Categories
The system categorizes all interactable objects into logical groups:
- **NPCs**: Characters and dialogue targets
- **Locations**: Doors, exits, area transitions
- **Loot**: Containers, items, skill orbs
- **Everything**: All available objects

## Game Integration Points

### Registry Access
The mod taps into `MouseOverHighlight.registry` to access all scene objects, bypassing the game's limited interaction range.

### Character Control Integration  
Uses `CharacterAnalogueControl` and its `InteractableSelectionManager` for player movement and object interaction.

### Unity UI Integration
Monitors `UnityEngine.EventSystems` and `Selectable` components to announce menu navigation.

## Development Workflow

### Testing
The game must be launched with MelonLoader installed to test the mod. Console output appears in MelonLoader's console window (usually F4).

### Debugging
- MelonLoader console shows all debug output
- Enable detailed logging in `AccessibilityMod.cs` if needed
- Use `;` key in-game to test object registry access
- Use `'` key to test distance calculations

### Game Assembly References
The mod references Il2Cpp-generated assemblies from the game installation. These are located at:
- `$DISCO_ELYSIUM_PATH/MelonLoader/Il2CppAssemblies/`
- `$DISCO_ELYSIUM_PATH/MelonLoader/net6/`

## Important Implementation Notes

### Screen Reader Integration
All user-facing announcements go through `TolkScreenReader.Instance.Speak()` which handles:
- Multiple screen reader compatibility (NVDA, JAWS, etc.)
- SAPI fallback when no screen reader is detected
- **Braille display support**: Automatically outputs to both speech and braille displays via `Tolk.Output()`

**Two output channels, on purpose.** The screen reader is one channel, and the mod's own announcements (navigation, keypress echoes, menu moves) constantly interrupt each other on it. **Orb text** - the atmospheric speech bubbles over the radio and the skills (`OrbTextVocalizationPatches`) - was drowned there, so it goes out on a *separate* voice through the external **orb TTS server** (see `tools/TtsServer` below). The mod is deliberately dumb about it: `mod/OrbSpeech.cs` hands the server `(speaker, text)` over a pipe and nothing else - it never learns which voice, engine, or volume is in play, or whether a line even made a sound. The server plays in parallel with NVDA/JAWS instead of competing for the one channel, and queues lines so two speakers close together are both heard. If the server cannot be started at all, `OrbSpeech.Speak` returns false and orb text falls back to the screen reader, so a line is never simply lost. Orb text is still recorded as heard via `TolkScreenReader.RecordExternalSpeech` (not `Speak`, which would route audio to the screen reader) so `SpeechLog.txt` and the debugger transcript stay complete. Note the tradeoff: orb no longer passes through the AudioAware queue, so it can overlap the game's Final Cut voice-acting - acceptable because orb rarely coincides with voiced lines and the alternative was losing it entirely.

**Area descriptions and introductions take the same channel** (`AccessibilityMod.SpeakDescriptionOnServerOrTolk`): they are long and atmospheric like orb text, and were cut off the same way on the screen reader. They go to the server with an empty speaker, same Tolk fallback, same `RecordExternalSpeech` bookkeeping. The auto-on-entry description also shares the orbs' **repeat-suppression window** (`AccessibilityPreferences.GetRepeatSuppressionSeconds`, set in minutes as `RepeatSuppressionMinutes`, one player-facing dial for both, 0 = off): a description that already played within the window does not replay when the player walks a door back and forth while searching a room. Asking for the description or the intro by key always speaks - an explicit request is never suppressed.

Why an external process and not an in-mod voice: the old orb voice was Windows SAPI via late-bound COM (`SAPI.SpVoice`), which can only reach the two legacy desktop voices. The *good* modern voices - the OneCore voices (Hedda, Katja, Stefan) and, once the user installs them, the natural/neural voices - are reachable only through WinRT (`Windows.Media.SpeechSynthesis`), whose runtime is a 25 MB projection and which needs to own audio playback. That weight and the future backends (ElevenLabs, pre-rendered WAV per speaker) do not belong inside the game process, so they live in the server. The screen reader (Tolk) deliberately stays in-process and unchanged: it is the fast, interruptible, braille-carrying critical path, and a server crash must never take it down.

### Il2Cpp Considerations
- Use `Il2Cpp*` prefixed types for game objects (e.g., `Il2CppTMPro.TextMeshProUGUI`)
- Unity interop requires special handling for some operations
- Game assemblies are IL2CPP-compiled, not standard .NET

### Performance
- Object scanning is optimized to avoid frame drops
- Distance calculations are cached where possible  
- UI monitoring uses minimal overhead polling

### Game Compatibility
Designed for **Disco Elysium - The Final Cut** with MelonLoader. The mod specifically targets the game's interaction and UI systems, so changes to game updates may require mod updates.
