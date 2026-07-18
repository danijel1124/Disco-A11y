using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using AccessibilityMod.Settings;
using AccessibilityMod.Utils;
using Il2Cpp;
using Il2CppSunshine;
using Il2CppSunshine.Metric;
using Il2CppSunshine.Views;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Everything around thought research completion and the thought cabinet's own
    /// storytelling (bug #57).
    ///
    /// The problem, as reported by the player: the mod announced "almost done" while a
    /// thought was cooking - and then NOTHING. The research result appeared only
    /// visually (a modal fullscreen splash, ThoughtSplashScreenView), its close button
    /// is mouse-only, and the view opens with no EventSystem selection (all verified
    /// live 17.07.2026). A keyboard player was trapped: they could still walk (the
    /// mod's world navigation does not go through the UI) but every interaction was
    /// swallowed by the invisible modal - "I can walk but not interact".
    ///
    /// AnnounceSplash (via the SetProject + OnEnable patches below) reads EVERYTHING the
    /// splash shows - title, the thought's own musing text, completion effect and the
    /// bonus list - not just the name. The keyboard close itself lives in
    /// InputManager.TryCloseThoughtSplash (Enter / the interact key), which is the single,
    /// verified-working exit path.
    ///
    /// The third half - "what the cabinet says about the thought" while BROWSING the
    /// cabinet - is deliberately NOT a patch here: ThoughtSlot.OnSelect is a virtual
    /// Il2Cpp method whose patching crashes the game (see the NOTE at the bottom of this
    /// file), so that narration lives in ThoughtCabinetNavigationHandler.CheckSelectedThought
    /// as a per-frame EventSystem poll instead.
    /// </summary>
    public static class ThoughtSplashAnnouncer
    {
        // SetProject and OnEnable both run when the splash opens (order depends on the
        // game's flow), and both call in here. Dedup on the THOUGHT, not on the rendered
        // text: the two calls can read the panel labels at different render stages, so
        // the text can differ even though it is the same thought - keying on text would
        // let that difference slip through and announce the same result twice. A
        // genuinely different thought (tabbing to another completed one via
        // ChangeShownThought -> SetProject) has a different key and re-announces.
        private static string lastAnnouncedThought = "";
        private static float lastAnnouncedTime;

        public static void AnnounceSplash(ThoughtSplashScreenView view)
        {
            try
            {
                if (view == null) return;
                var project = view.currentProject;
                if (project == null) return; // splash without a thought = nothing to read

                var parts = new List<string>();

                // Title: prefer what the panel actually renders (localized by the game),
                // fall back to the data model if the text field is not filled yet.
                string title = view.titleText != null && !string.IsNullOrWhiteSpace(view.titleText.text)
                    ? view.titleText.text
                    : project.displayName;
                parts.Add(Loc.Get("ThoughtCompletedNoEffect", RTLHelper.FixForScreenReader(title)));

                // The thought's own musing text ("der Gedanke") - the story the game's
                // narrator voice reads on this screen. First live test 17.07.: the
                // player heard the bonuses but reported the THOUGHT itself missing -
                // this is that text. Model data, localized by the game.
                if (!string.IsNullOrWhiteSpace(project.description))
                {
                    parts.Add(RTLHelper.FixForScreenReader(project.description.Trim()));
                }

                // The permanent effect ("Effekt: ...") - the payoff the player waited
                // hours of game time for. Panel text first, model fallback again.
                string completion = view.completionDescriptionText != null && !string.IsNullOrWhiteSpace(view.completionDescriptionText.text)
                    ? view.completionDescriptionText.text
                    : project.completionDescription;
                if (!string.IsNullOrWhiteSpace(completion))
                {
                    parts.Add(RTLHelper.FixForScreenReader(completion.Trim()));
                }

                // The bonus list (propertiesText, e.g. "+1 Logik: ...") - a sighted
                // player reads it off the panel; without this line a blind player never
                // learns the numbers. Only exists on the panel, no model fallback.
                if (view.propertiesText != null && !string.IsNullOrWhiteSpace(view.propertiesText.text))
                {
                    parts.Add(RTLHelper.FixForScreenReader(view.propertiesText.text.Replace("\n", ". ").Trim()));
                }

                // The exit hint is PART of the same utterance, not a second Speak call:
                // a queued follow-up line gets promoted to interrupting when the player's
                // global speech-interrupt setting is on, and beheaded the whole result
                // read after ~0 ms (PR review finding 3). One utterance cannot interrupt
                // itself. The hint renders the LIVE binding (finding 10) - remapping the
                // key updates the spoken text automatically.
                parts.Add(Loc.Get("SplashCloseHint", KeyBindings.SpeakableName(GameKey.CloseSplash)));

                string announcement = string.Join(" ", parts);

                // Same thought within 2s = the second patch of the pair firing (or the
                // game re-running SetProject), not new info. Keyed on the thought's own
                // name, so a differently-rendered-but-same thought is still deduped.
                string thoughtKey = project.displayName ?? "";
                if (thoughtKey == lastAnnouncedThought && UnityEngine.Time.unscaledTime - lastAnnouncedTime < 2f) return;
                lastAnnouncedThought = thoughtKey;
                lastAnnouncedTime = UnityEngine.Time.unscaledTime;

                // Interrupting on purpose: this is a modal takeover the player must
                // acknowledge - exactly the moment to stop whatever else was talking.
                // (Content + exit hint travel in this ONE call, see above.)
                TolkScreenReader.Instance.Speak(announcement, true);
                MelonLogger.Msg($"[THOUGHT] Splash announced: {project.displayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing thought splash: {ex}");
            }
        }
    }

    /// <summary>
    /// SetProject is the game's own "this splash is about this thought" call - it fires
    /// both when the splash first opens and when the player tabs to another completed
    /// thought (ChangeShownThought). Announcing here covers both.
    /// </summary>
    [HarmonyPatch(typeof(ThoughtSplashScreenView), "SetProject")]
    public static class ThoughtSplashScreen_SetProject_Patch
    {
        public static void Postfix(ThoughtSplashScreenView __instance, ThoughtCabinetProject project)
        {
            ThoughtSplashAnnouncer.AnnounceSplash(__instance);
        }
    }

    /// <summary>
    /// OnEnable = the splash is actually visible now. Announce the content (this also
    /// covers the flow where SetProject ran before the panel texts were filled - here
    /// they are final).
    ///
    /// We deliberately do NOT select the close button here. An earlier version did, to
    /// let Unity's Submit route Enter to the button - but the keyboard close is handled
    /// explicitly in InputManager.TryCloseThoughtSplash (which invokes buttonClose.onClick
    /// itself), so selecting the button as well would give Enter two paths to onClick and
    /// could run the accept bookkeeping (SetThoughtStateAndGoBack) twice per press
    /// (raised in PR review). One path only: the explicit one, which is the verified-
    /// working close (Unity's Submit alone did not close the splash in live testing).
    /// </summary>
    [HarmonyPatch(typeof(ThoughtSplashScreenView), "OnEnable")]
    public static class ThoughtSplashScreen_OnEnable_Patch
    {
        public static void Postfix(ThoughtSplashScreenView __instance)
        {
            ThoughtSplashAnnouncer.AnnounceSplash(__instance);
        }
    }

    // NOTE - no Harmony patch for "what the cabinet says about the thought":
    // ThoughtSlot.OnSelect is a VIRTUAL Il2Cpp method, and patching those crashes the
    // game natively (learned the hard way 17.07.2026 - instant process death on the
    // first slot selection; the project worklog #30 documents the same failure mode
    // for OneAxisInputControl.get_WasPressed). The cabinet narration lives in
    // ThoughtCabinetNavigationHandler.CheckSelectedThought instead: a per-frame poll
    // of the EventSystem selection, the same safe pattern the rest of the mod uses.
}
