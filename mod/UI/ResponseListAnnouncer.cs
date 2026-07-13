using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using AccessibilityMod.Settings;

namespace AccessibilityMod.UI
{
    /// <summary>
    /// Reads out the dialogue options as soon as they appear, numbered.
    ///
    /// This is what left players stuck in conversations: the mod only ever spoke a
    /// response when the EventSystem had it selected, which happens with a controller
    /// or arrow-key navigation - but never in plain keyboard play, where nothing is
    /// selected at all. So the lines were read, then silence, and the three options
    /// waiting at the bottom of the screen were invisible. Worse, the first option is
    /// often a loop back into the same line ("You pull the fan" - "the switch must be
    /// broken"), which is exactly what a blind player hitting Enter would trigger over
    /// and over.
    ///
    /// The numbers are the game's own dialogue shortcut keys (1-9), so "press 3" is
    /// something the player can act on immediately.
    /// </summary>
    public static class ResponseListAnnouncer
    {
        private const float POLL_INTERVAL = 0.25f;

        // Options fade in one by one; wait for the set to stop changing before speaking,
        // otherwise we announce a half-built list and then repeat ourselves.
        private const float SETTLE_TIME = 0.35f;

        private static float lastPoll;
        private static string lastAnnounced = "";
        private static string pendingSignature = "";
        private static float pendingSince;

        public static void Update()
        {
            if (Time.unscaledTime - lastPoll < POLL_INTERVAL) return;
            lastPoll = Time.unscaledTime;

            try
            {
                var options = CollectVisibleOptions();

                if (options.Count == 0)
                {
                    lastAnnounced = "";
                    pendingSignature = "";
                    return;
                }

                string signature = string.Join("|", options);

                if (signature != pendingSignature)
                {
                    pendingSignature = signature;
                    pendingSince = Time.unscaledTime;
                    return;
                }

                if (Time.unscaledTime - pendingSince < SETTLE_TIME) return;
                if (signature == lastAnnounced) return;

                lastAnnounced = signature;
                Announce(options);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[RESPONSES] {ex.Message}");
            }
        }

        private static List<string> CollectVisibleOptions()
        {
            var options = new List<(int Index, string Text)>();

            foreach (var button in UnityEngine.Object.FindObjectsOfType<Il2Cpp.SunshineResponseButton>())
            {
                if (button == null || !button.gameObject.activeInHierarchy) continue;

                string text = GetOptionText(button);
                if (string.IsNullOrWhiteSpace(text)) continue;

                options.Add((button.transform.GetSiblingIndex(), text));
            }

            // Siblings are in screen order, which is the order the shortcut keys follow.
            return options.OrderBy(o => o.Index).Select(o => o.Text).ToList();
        }

        private static string GetOptionText(Il2Cpp.SunshineResponseButton button)
        {
            var option = button.optionText;
            if (option == null) return null;

            string text = option.textField != null ? option.textField.text : null;
            if (string.IsNullOrWhiteSpace(text)) text = option.originalText;
            if (string.IsNullOrWhiteSpace(text)) return null;

            // The buttons render their own "1. " prefix into the text on some layouts;
            // we number them ourselves, so drop it rather than say "one one".
            text = text.Trim();
            int dot = text.IndexOf(". ", StringComparison.Ordinal);
            if (dot > 0 && dot <= 2 && char.IsDigit(text[0]))
            {
                text = text.Substring(dot + 2).Trim();
            }

            return Utils.RTLHelper.FixForScreenReader(text);
        }

        private static void Announce(List<string> options)
        {
            var parts = new List<string> { Loc.Get(options.Count == 1 ? "ResponseOne" : "ResponseMany", options.Count) };

            for (int i = 0; i < options.Count; i++)
            {
                parts.Add($"{i + 1}. {options[i]}");
            }

            parts.Add(Loc.Get("ResponseHowTo"));

            // Queued, never interrupting: the dialogue line that leads into these options
            // is usually still being read, and cutting it off to list the options would
            // lose the very context you need to choose between them.
            string announcement = string.Join(" ", parts);
            TolkScreenReader.Instance.Speak(announcement, false, AnnouncementCategory.Queueable);
            MelonLogger.Msg($"[RESPONSES] {options.Count} options: {string.Join(" | ", options)}");
        }
    }
}
