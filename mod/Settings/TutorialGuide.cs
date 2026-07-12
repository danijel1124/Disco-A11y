using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace AccessibilityMod.Settings
{
    /// <summary>
    /// Contextual one-shot tutorial tips: each fires exactly once (persisted), always
    /// queued behind current speech, and only when the situation makes the feature
    /// relevant right now. Disable entirely via the TutorialTips preference.
    /// </summary>
    public static class TutorialGuide
    {
        private static MelonPreferences_Category category;
        private static MelonPreferences_Entry<string> seenEntry;
        private static readonly HashSet<string> seen = new();
        private static float lastPoll;
        private static int lastLineCounter;

        public static void Initialize()
        {
            category = MelonPreferences.CreateCategory("Tutorial");
            category.SetFilePath("UserData/AccessibilityMod.cfg");
            seenEntry = category.CreateEntry<string>("SeenTips", "", "Comma-separated ids of tips already played");
            foreach (var id in seenEntry.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                seen.Add(id);
            }
            category.SaveToFile();

            Show("Welcome", () => Loc.Get("Tip_Welcome", "F1"));
        }

        /// <summary>Polled triggers; called every frame from OnUpdate.</summary>
        public static void Update()
        {
            if (!AccessibilityPreferences.GetTutorialTips()) return;
            if (Time.unscaledTime - lastPoll < 2f) return;
            lastPoll = Time.unscaledTime;

            try
            {
                // A freshly rendered dialogue line is the reliable "conversation is
                // happening" signal - DialogStateManager's isInConversation flag misses
                // some flows (verified: the intro dialogue leaves it false).
                bool dialogueActive = Patches.DialogSystemPatches.LineCounter != lastLineCounter;
                lastLineCounter = Patches.DialogSystemPatches.LineCounter;
                if (dialogueActive
                    && UI.DialogStateManager.CurrentDialogMode == UI.DialogReadingMode.Disabled)
                {
                    Show("Conversation", () => Loc.Get("Tip_Conversation",
                        KeyBindings.SpeakableName(GameKey.ToggleDialogReading),
                        KeyBindings.SpeakableName(GameKey.ToggleDialogAutoAdvance)));
                }

                var registry = Il2CppFortressOccident.MouseOverHighlight.registry;
                if (registry != null && registry.Count > 0)
                {
                    Show("Objects", () => Loc.Get("Tip_Objects",
                        KeyBindings.SpeakableName(GameKey.SelectNpcs),
                        KeyBindings.SpeakableName(GameKey.SelectLocations),
                        KeyBindings.SpeakableName(GameKey.SelectLoot),
                        KeyBindings.SpeakableName(GameKey.CycleForward),
                        KeyBindings.SpeakableName(GameKey.CycleBackward),
                        KeyBindings.SpeakableName(GameKey.NavigateToSelected)));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TUTORIAL] {ex.Message}");
            }
        }

        /// <summary>Called by SmartNavigationSystem after auto-walk reaches a world object.</summary>
        public static void OnArrivalAtObject()
        {
            if (AccessibilityPreferences.GetAutoInteract()) return;
            Show("Arrival", () => Loc.Get("Tip_Arrival",
                KeyBindings.SpeakableName(GameKey.InteractWithSelected),
                KeyBindings.SpeakableName(GameKey.ToggleAutoInteract)));
        }

        private static void Show(string id, Func<string> text)
        {
            if (!AccessibilityPreferences.GetTutorialTips()) return;
            if (!seen.Add(id)) return;

            seenEntry.Value = string.Join(",", seen);
            category.SaveToFile();

            TolkScreenReader.Instance.Speak(text(), false, AnnouncementCategory.Queueable);
            MelonLogger.Msg($"[TUTORIAL] Tip played: {id}");
        }
    }
}
