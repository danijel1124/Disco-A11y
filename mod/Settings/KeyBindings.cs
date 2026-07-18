using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace AccessibilityMod.Settings
{
    public readonly struct KeyBinding
    {
        public readonly KeyCode Key;
        public readonly bool RequireCtrl;
        public readonly bool RequireAlt;
        public readonly bool RequireShift;

        public KeyBinding(KeyCode key, bool requireCtrl = false, bool requireAlt = false, bool requireShift = false)
        {
            Key = key;
            RequireCtrl = requireCtrl;
            RequireAlt = requireAlt;
            RequireShift = requireShift;
        }

        public string Serialize() => $"{Key}|{RequireCtrl}|{RequireAlt}|{RequireShift}";

        public static KeyBinding Deserialize(string value, KeyBinding fallback)
        {
            var parts = value?.Split('|');
            if (parts == null || parts.Length != 4
                || !Enum.TryParse<KeyCode>(parts[0], out var key)
                || !bool.TryParse(parts[1], out var ctrl)
                || !bool.TryParse(parts[2], out var alt)
                || !bool.TryParse(parts[3], out var shift))
            {
                return fallback;
            }

            return new KeyBinding(key, ctrl, alt, shift);
        }

        public string Describe()
        {
            var mods = "";
            if (RequireCtrl) mods += "Ctrl+";
            if (RequireAlt) mods += "Alt+";
            if (RequireShift) mods += "Shift+";
            return mods + Key;
        }
    }

    /// <summary>
    /// Configurable keyboard bindings for every accessibility hotkey. Defaults reproduce
    /// the mod's original hardcoded US-QWERTY punctuation bindings (LeftBracket, Backslash,
    /// etc.), which are bound to physical key *position* on US layouts and land on
    /// different / AltGr-gated keys on non-US layouts (e.g. German QWERTZ). The "NumpadSafe"
    /// preset rebinds the layout-sensitive actions onto the numeric keypad, whose layout is
    /// standardized worldwide, so it works regardless of the active keyboard layout.
    ///
    /// Bindings are persisted via MelonPreferences to UserData/AccessibilityMod.cfg, in the
    /// same file as the mod's other settings, under the "KeyBindings" category. An external
    /// keybind editor can rewrite that file directly (same key/value format written here);
    /// the mod re-reads it on next launch.
    /// </summary>
    public static class KeyBindings
    {
        private static MelonPreferences_Category category;
        private static readonly Dictionary<GameKey, MelonPreferences_Entry<string>> entries = new();
        private static readonly Dictionary<GameKey, KeyBinding> current = new();

        public static IReadOnlyDictionary<GameKey, KeyBinding> Defaults { get; } = new Dictionary<GameKey, KeyBinding>
        {
            [GameKey.AnnounceCurrentSelection] = new KeyBinding(KeyCode.BackQuote),
            [GameKey.ToggleSortingMode] = new KeyBinding(KeyCode.Semicolon),
            [GameKey.ScanSceneByDistance] = new KeyBinding(KeyCode.Quote),

            [GameKey.SelectNpcs] = new KeyBinding(KeyCode.LeftBracket),
            [GameKey.SelectLocations] = new KeyBinding(KeyCode.RightBracket),
            [GameKey.SelectLoot] = new KeyBinding(KeyCode.Backslash),
            [GameKey.SelectEverything] = new KeyBinding(KeyCode.Equals),

            [GameKey.FocusWaypoints] = new KeyBinding(KeyCode.LeftBracket, requireCtrl: true),
            [GameKey.CreateWaypoint] = new KeyBinding(KeyCode.LeftBracket, requireAlt: true),
            [GameKey.DeleteWaypoint] = new KeyBinding(KeyCode.RightBracket, requireAlt: true),

            [GameKey.CycleForward] = new KeyBinding(KeyCode.Period),
            [GameKey.CycleBackward] = new KeyBinding(KeyCode.Period, requireShift: true),
            // stardew-access: left ctrl + pageDown/pageUp = next/previous category.
            [GameKey.CycleCategoryForward] = new KeyBinding(KeyCode.PageDown, requireCtrl: true),
            [GameKey.CycleCategoryBackward] = new KeyBinding(KeyCode.PageUp, requireCtrl: true),
            [GameKey.NavigateToSelected] = new KeyBinding(KeyCode.Comma),
            // F is free in the game (its own E-Interact only works with the
            // controller-selection flow, which keyboard play never populates).
            [GameKey.InteractWithSelected] = new KeyBinding(KeyCode.F),
            [GameKey.ToggleAutoInteract] = new KeyBinding(KeyCode.F, requireCtrl: true),
            [GameKey.StopMovement] = new KeyBinding(KeyCode.Slash),

            [GameKey.ToggleDialogReading] = new KeyBinding(KeyCode.Minus),
            // G is free in the game's default bindings (checked via the conflict dump);
            // unique base key required, see IsPressed.
            [GameKey.ToggleDialogAutoAdvance] = new KeyBinding(KeyCode.G),
            [GameKey.RepeatDialogue] = new KeyBinding(KeyCode.R),
            [GameKey.ToggleOrbAnnouncements] = new KeyBinding(KeyCode.Alpha0),
            [GameKey.ToggleSpeechInterrupt] = new KeyBinding(KeyCode.Alpha8),
            [GameKey.ToggleDiagnostics] = new KeyBinding(KeyCode.Alpha9, requireCtrl: true),

            [GameKey.AnnounceStatus] = new KeyBinding(KeyCode.H),
            [GameKey.AnnounceStats] = new KeyBinding(KeyCode.X),
            [GameKey.AnnounceOfficerProfile] = new KeyBinding(KeyCode.O),
            [GameKey.ReadSkillDescription] = new KeyBinding(KeyCode.N),
            [GameKey.AnnounceKimStatus] = new KeyBinding(KeyCode.K),
            // Ctrl+N: free in the game's own bindings, and next to the plain N that reads
            // a skill description - both are "tell me more about what is selected".
            [GameKey.AnnounceNameSources] = new KeyBinding(KeyCode.N, requireCtrl: true),
            // U: free in the game's own bindings (its L, J, C, T, I, M and friends are all
            // taken) and in the same physical spot on QWERTY and QWERTZ.
            [GameKey.DescribeArea] = new KeyBinding(KeyCode.U),
            [GameKey.DescribeAreaFull] = new KeyBinding(KeyCode.U, requireCtrl: true),
            // B: free in the game's own bindings, and it is the same key in every preset -
            // "describe what I have selected" is not something to hunt for.
            [GameKey.DescribeItem] = new KeyBinding(KeyCode.B),
            // Ctrl+Y: the mod debugger window. Diagnostic, so it hides behind Ctrl and
            // behind the debug-mode switch.
            [GameKey.OpenModDebugger] = new KeyBinding(KeyCode.Y, requireCtrl: true),

            // Ctrl+Tab / Ctrl+Shift+Tab: the universal "switch tab" convention (user
            // decision). Plain Tab stays the game's own highlight key - the Ctrl
            // requirement keeps these from ever colliding with it.
            [GameKey.InventoryNextTab] = new KeyBinding(KeyCode.Tab, requireCtrl: true),
            [GameKey.InventoryPrevTab] = new KeyBinding(KeyCode.Tab, requireCtrl: true, requireShift: true),

            // Healing (health/morale bar plus buttons, mouse-only in the game).
            // Two dead ends before this binding, both documented so nobody walks back in:
            //  1. KeyCode.Plus does NOT fire Input.GetKeyDown on a German QWERTZ layout
            //     (verified live 17.07.2026 - the mod never saw the key). Punctuation
            //     keycodes are layout-bound, exactly the trap this project moved away from.
            //  2. Ctrl+1/Ctrl+2 (digits) fire on every layout - but the GAME reads the
            //     digit keys during conversations REGARDLESS of held Ctrl (worklog: "plain
            //     8 also picks dialogue option 8"). A habitual Ctrl+1 mid-dialogue would
            //     silently commit dialogue option 1 - an unrecoverable story decision
            //     (PR review finding 1). Digits are therefore off limits for mod hotkeys.
            // H is the status key that already answers "how are health and morale?", so
            // healing lives on its modifiers (user decision 18.07.2026): Ctrl+H heals
            // health, Shift+H heals morale. The game does not read H in dialogue. Plain H
            // stays AnnounceStatus - HandleDialogSafeKeys yields it to these two chords
            // (specific-binding-first rule, see IsPressed).
            [GameKey.HealHealth] = new KeyBinding(KeyCode.H, requireCtrl: true),
            [GameKey.HealMorale] = new KeyBinding(KeyCode.H, requireShift: true),

            // Enter closes the thought-research splash (its own close button is mouse-only).
            // Remappable like every other hotkey (PR review finding 10); the spoken hint
            // (SplashCloseHint) renders the LIVE binding via SpeakableName, never a
            // hardcoded key name.
            [GameKey.CloseSplash] = new KeyBinding(KeyCode.Return),
        };

        /// <summary>
        /// Layout-independent preset for keyboards WITH a numpad. Rebuilt after checking
        /// the game's own live keyboard bindings (see GameKeybindConflictChecker):
        /// Keypad1-9 pick dialogue options during conversations (DialogueShortKeys), so
        /// only collision-free numpad keys remain here (Keypad0, KeypadDivide); the
        /// category/scan actions live on free F-keys instead (F1=help, F5=quicksave,
        /// F9=quickload belong to the game). Waypoint actions are Ctrl/Alt variants of
        /// their category keys. StopMovement is Space - the game's own stop key, so mod
        /// and game stop consistently together. Cycle/navigate keep stardew-access's
        /// conventions (Page Up/Down, Ctrl+Home). Letter keys (R, H, X, O, N, K, Alpha0)
        /// stay on the upstream defaults; ToggleSpeechInterrupt moved off Alpha8 because
        /// plain 8 also picks dialogue option 8.
        /// </summary>
        public static IReadOnlyDictionary<GameKey, KeyBinding> NumpadSafePreset { get; } = new Dictionary<GameKey, KeyBinding>
        {
            [GameKey.AnnounceCurrentSelection] = new KeyBinding(KeyCode.F6),
            [GameKey.ToggleSortingMode] = new KeyBinding(KeyCode.F7),
            [GameKey.ScanSceneByDistance] = new KeyBinding(KeyCode.F8),

            [GameKey.SelectNpcs] = new KeyBinding(KeyCode.F2),
            [GameKey.SelectLocations] = new KeyBinding(KeyCode.F3),
            [GameKey.SelectLoot] = new KeyBinding(KeyCode.F4),
            [GameKey.SelectEverything] = new KeyBinding(KeyCode.Keypad0),

            [GameKey.FocusWaypoints] = new KeyBinding(KeyCode.F2, requireCtrl: true),
            [GameKey.CreateWaypoint] = new KeyBinding(KeyCode.F2, requireAlt: true),
            [GameKey.DeleteWaypoint] = new KeyBinding(KeyCode.F3, requireAlt: true),

            // stardew-access: pageDown/pageUp = next/previous object.
            [GameKey.CycleForward] = new KeyBinding(KeyCode.PageDown),
            [GameKey.CycleBackward] = new KeyBinding(KeyCode.PageUp),
            // stardew-access: left ctrl + pageDown/pageUp = next/previous category.
            [GameKey.CycleCategoryForward] = new KeyBinding(KeyCode.PageDown, requireCtrl: true),
            [GameKey.CycleCategoryBackward] = new KeyBinding(KeyCode.PageUp, requireCtrl: true),
            // stardew-access: left ctrl + home = move to selected object.
            [GameKey.NavigateToSelected] = new KeyBinding(KeyCode.Home, requireCtrl: true),
            [GameKey.InteractWithSelected] = new KeyBinding(KeyCode.F),
            [GameKey.ToggleAutoInteract] = new KeyBinding(KeyCode.F, requireCtrl: true),
            // Space is the game's own built-in stop key.
            [GameKey.StopMovement] = new KeyBinding(KeyCode.Space),

            [GameKey.ToggleDialogReading] = new KeyBinding(KeyCode.KeypadDivide),
            [GameKey.ToggleDialogAutoAdvance] = new KeyBinding(KeyCode.G),
            [GameKey.RepeatDialogue] = new KeyBinding(KeyCode.R),
            [GameKey.ToggleOrbAnnouncements] = new KeyBinding(KeyCode.Alpha0),
            [GameKey.ToggleSpeechInterrupt] = new KeyBinding(KeyCode.F11),
            [GameKey.ToggleDiagnostics] = new KeyBinding(KeyCode.B, requireCtrl: true),

            [GameKey.AnnounceStatus] = new KeyBinding(KeyCode.H),
            [GameKey.AnnounceStats] = new KeyBinding(KeyCode.X),
            [GameKey.AnnounceOfficerProfile] = new KeyBinding(KeyCode.O),
            [GameKey.ReadSkillDescription] = new KeyBinding(KeyCode.N),
            [GameKey.AnnounceKimStatus] = new KeyBinding(KeyCode.K),
            // Ctrl+N: free in the game's own bindings, and next to the plain N that reads
            // a skill description - both are "tell me more about what is selected".
            [GameKey.AnnounceNameSources] = new KeyBinding(KeyCode.N, requireCtrl: true),
            // U: free in the game's own bindings (its L, J, C, T, I, M and friends are all
            // taken) and in the same physical spot on QWERTY and QWERTZ.
            [GameKey.DescribeArea] = new KeyBinding(KeyCode.U),
            [GameKey.DescribeAreaFull] = new KeyBinding(KeyCode.U, requireCtrl: true),
            // B: free in the game's own bindings, and it is the same key in every preset -
            // "describe what I have selected" is not something to hunt for.
            [GameKey.DescribeItem] = new KeyBinding(KeyCode.B),
            // Ctrl+Y: the mod debugger window. Diagnostic, so it hides behind Ctrl and
            // behind the debug-mode switch.
            [GameKey.OpenModDebugger] = new KeyBinding(KeyCode.Y, requireCtrl: true),

            // Same in every preset - see Defaults for the reasoning.
            [GameKey.InventoryNextTab] = new KeyBinding(KeyCode.Tab, requireCtrl: true),
            [GameKey.InventoryPrevTab] = new KeyBinding(KeyCode.Tab, requireCtrl: true, requireShift: true),
            [GameKey.HealHealth] = new KeyBinding(KeyCode.H, requireCtrl: true),
            [GameKey.HealMorale] = new KeyBinding(KeyCode.H, requireShift: true),
            [GameKey.CloseSplash] = new KeyBinding(KeyCode.Return),
        };

        /// <summary>
        /// Stardew-like preset: identical to NumpadSafePreset except that it uses NO
        /// numpad key at all (rule: must work on keyboards without a numpad, e.g.
        /// laptops). The two numpad keys of the other preset move to the remaining free
        /// F-keys: SelectEverything to F10, ToggleDialogReading to F12. Each action keeps
        /// a unique base key because IsPressed tolerates extra held modifiers - two
        /// actions sharing a base key outside the category if/else-if chain would fire
        /// together.
        /// </summary>
        public static IReadOnlyDictionary<GameKey, KeyBinding> StardewPreset { get; } = BuildStardewPreset();

        private static IReadOnlyDictionary<GameKey, KeyBinding> BuildStardewPreset()
        {
            var preset = new Dictionary<GameKey, KeyBinding>(
                (IDictionary<GameKey, KeyBinding>)NumpadSafePreset);
            preset[GameKey.SelectEverything] = new KeyBinding(KeyCode.F10);
            preset[GameKey.ToggleDialogReading] = new KeyBinding(KeyCode.F12);
            return preset;
        }

        public static void Initialize()
        {
            category = MelonPreferences.CreateCategory("KeyBindings");
            category.SetFilePath("UserData/AccessibilityMod.cfg");

            foreach (var kvp in Defaults)
            {
                var entry = category.CreateEntry<string>(kvp.Key.ToString(), kvp.Value.Serialize());
                entries[kvp.Key] = entry;
                current[kvp.Key] = KeyBinding.Deserialize(entry.Value, kvp.Value);
            }

            // MelonPreferences only writes the file to disk on an explicit save (or once a
            // value changes) - force one now so the config always exists with the current
            // bindings after startup, even before the player rebinds anything. An external
            // keybind editor can then rely on the file being present.
            category.SaveToFile();

            MelonLogger.Msg("[KEYBINDINGS] Loaded from UserData/AccessibilityMod.cfg");
        }

        public static KeyBinding Get(GameKey action) => current[action];

        /// <summary>
        /// Speech-friendly name of the key currently bound to an action, for use in
        /// spoken hint texts ("Press X to ..."). Always reflects the live binding, so
        /// hints stay correct after remapping - never hardcode key names in
        /// announcements. "Ctrl+PageDown" becomes "Ctrl plus Page Down".
        /// </summary>
        public static string SpeakableName(GameKey action)
        {
            var described = Get(action).Describe().Replace("+", " plus ");
            // Split lower->upper and digit->letter boundaries but never digit->digit,
            // otherwise F12 becomes "F1 2".
            return System.Text.RegularExpressions.Regex.Replace(described, "(?<=[a-z])(?=[A-Z0-9])|(?<=[0-9])(?=[A-Z])", " ");
        }

        public static void Set(GameKey action, KeyBinding binding)
        {
            current[action] = binding;
            entries[action].Value = binding.Serialize();
            category.SaveToFile();
        }

        public static void ApplyPreset(IReadOnlyDictionary<GameKey, KeyBinding> preset)
        {
            foreach (var kvp in preset)
            {
                current[kvp.Key] = kvp.Value;
                entries[kvp.Key].Value = kvp.Value.Serialize();
            }
            category.SaveToFile();
        }

        /// <summary>
        /// True on the frame the action's bound key was pressed, with at least the
        /// binding's own required modifiers held - an unrelated extra modifier held for
        /// some other reason (resting a hand on Ctrl, Sticky Keys, etc.) doesn't block an
        /// otherwise-unmodified hotkey, matching how these hotkeys behaved before they
        /// were made remappable. Where several GameKeys share a physical key with
        /// different modifier requirements (e.g. SelectNpcs / FocusWaypoints /
        /// CreateWaypoint can all sit on LeftBracket), this alone doesn't disambiguate
        /// them when more modifiers are held than the least-specific binding needs - the
        /// caller's if/else-if chain must check the more specific (more required
        /// modifiers) binding first so ties resolve to the more specific action, exactly
        /// as the original hardcoded chain did. See InputManager.HandleInput.
        /// </summary>
        public static bool IsPressed(GameKey action)
        {
            var binding = current[action];
            if (!UnityEngine.Input.GetKeyDown(binding.Key))
            {
                return false;
            }

            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            bool alt = UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            return (!binding.RequireCtrl || ctrl) && (!binding.RequireAlt || alt) && (!binding.RequireShift || shift);
        }
    }
}
