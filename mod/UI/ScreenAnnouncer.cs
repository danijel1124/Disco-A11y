using System;
using Il2CppSunshine.Views;
using MelonLoader;
using UnityEngine;
using AccessibilityMod.Settings;

namespace AccessibilityMod.UI
{
    /// <summary>
    /// Says which screen you just opened, and the numbers that only exist at screen level.
    ///
    /// The existing patches announce the focused *element* well (a skill, a thought, a
    /// task), but never the screen around it: opening the character sheet read out
    /// "Encyclopedia +5..." without ever saying you were in the character sheet - or that
    /// you had skill points waiting to be spent. Sighted players read that off the panel;
    /// blind players had no way to learn it, and no way to report it missing either.
    ///
    /// Keyed off ViewController.GetCurrentView(), the game's own "which screen am I on"
    /// state, so this holds for every screen rather than a list of the ones we tested.
    /// (The DiscoPages page system that also ships in the build is dead code on PC -
    /// FindObjectsOfType&lt;DiscoPage&gt; returns 0 even with the character sheet open.)
    /// Numbers come from PlayerCharacter, the game's own model, not from scraping labels.
    /// </summary>
    public static class ScreenAnnouncer
    {
        private const float POLL_INTERVAL = 0.3f;

        private static float lastPoll;
        private static string lastViewName = "";

        public static void Update()
        {
            if (Time.unscaledTime - lastPoll < POLL_INTERVAL) return;
            lastPoll = Time.unscaledTime;

            View view;
            ViewType viewType;
            try
            {
                view = ViewController.GetCurrentView();
                if (view == null)
                {
                    lastViewName = "";
                    return;
                }

                // Il2Cpp hands back the base View type, so the concrete class name is
                // useless here - the game's own ViewType enum is the reliable identity.
                // Between scenes the current view is a stale wrapper whose type throws;
                // that is normal, not an error worth logging every frame.
                viewType = view.GetViewType();
            }
            catch
            {
                lastViewName = "";
                return;
            }

            try
            {
                string viewName = viewType.ToString();

                if (viewName == lastViewName) return;
                lastViewName = viewName;

                // The game switches views constantly while you just play (CLEAR, SPECIAL,
                // DIALOGUE, CUTSCENE...). Only the screens a player deliberately opens are
                // worth saying out loud - those are exactly the ones with a name below.
                // In debug mode the internal ones are announced too, raw: when you are
                // working on the mod, "the game is in SPECIAL" is exactly what you need to
                // hear - that is the boot screen, and it is why a load hangs there.
                if (!IsPlayerScreen(viewType))
                {
                    if (AccessibilityPreferences.GetDebugMode())
                    {
                        TolkScreenReader.Instance.Speak(
                            Loc.Get("DebugScreen", viewName), false, AnnouncementCategory.Queueable);
                    }
                    return;
                }

                Announce(view, viewType);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SCREEN] {ex.Message}");
            }
        }

        private static void Announce(View view, ViewType viewType)
        {
            string title = GetScreenName(viewType);
            string extra = GetScreenContext(viewType);

            string announcement = string.IsNullOrEmpty(extra) ? title : $"{title}. {extra}";
            // The thought splash announces its full research result the moment it opens
            // (ThoughtCompletionPatches) - a long, interrupting read. The screen NAME
            // must queue behind it, not behead it after 300 ms - and it needs the REAL
            // queue guarantee: Speak(interrupt: false) gets promoted to interrupting
            // when the player's global speech-interrupt setting is on and would still
            // cut the result read (PR review finding 3). Every other screen keeps
            // interrupting: there the name IS the primary information.
            if (viewType == ViewType.THOUGHTSPLASHSCREEN)
                TolkScreenReader.Instance.SpeakNeverInterrupt(announcement);
            else
                TolkScreenReader.Instance.Speak(announcement, true);
            MelonLogger.Msg($"[SCREEN] Opened: {viewType} -> {announcement}");
        }

        /// <summary>
        /// The screen's own heading, in the player's language, taken from the title label
        /// the game renders ("GEDANKENKABINETT"). Falls back to the class name so a screen
        /// without a heading is still announced rather than silently skipped.
        /// </summary>
        /// <summary>Screens a player opens on purpose - the ones worth announcing.
        /// THOUGHTSPLASHSCREEN is the exception to "opens on purpose": the GAME opens it
        /// when a thought finishes cooking, which is precisely why it must be announced -
        /// a modal fullscreen the player never asked for is otherwise a silent trap
        /// (bug #57b).</summary>
        private static bool IsPlayerScreen(ViewType viewType) =>
            viewType == ViewType.INVENTORY ||
            viewType == ViewType.CHARACTERSHEET ||
            viewType == ViewType.THOUGHTCABINET ||
            viewType == ViewType.THOUGHTSPLASHSCREEN ||
            viewType == ViewType.JOURNAL ||
            viewType == ViewType.OPTIONS ||
            viewType == ViewType.SAVE ||
            viewType == ViewType.LOAD ||
            viewType == ViewType.HELPOVERLAY;

        private static string GetScreenName(ViewType viewType) => Loc.Get("Screen_" + viewType);

        /// <summary>
        /// Screen-level numbers a blind player would otherwise never hear. Read from the
        /// game's own PlayerCharacter model, so they stay right even when the panel that
        /// displays them is off screen.
        /// </summary>
        /// <summary>
        /// How many items you are carrying, summed over all inventory tabs. Counted from
        /// InventoryViewData.tabContents, the game's own per-tab item model. (The previous
        /// version counted DiscoPages InventoryItemSlot components - a page system that is
        /// dead code on PC, so the count was always zero: "Inventar. 0 Gegenstände.")
        /// </summary>
        private static int CountCarriedItems()
        {
            try
            {
                var tabs = Il2CppSunshine.Metric.InventoryViewData.Singleton?.tabContents;
                if (tabs == null) return -1;

                int count = 0;
                foreach (var tab in tabs)
                {
                    if (tab.Value != null) count += tab.Value.Count;
                }
                return count;
            }
            catch
            {
                return -1;
            }
        }

        private static string GetScreenContext(ViewType viewType)
        {
            try
            {
                var player = Il2CppSunshine.Metric.PlayerCharacter.Singleton;
                if (player == null) return "";

                string context = "";

                // Unspent skill points are the one thing on these screens you must act on,
                // and nothing ever said them out loud.
                int skillPoints = player.SkillPoints;
                if (skillPoints > 0 &&
                    (viewType == ViewType.CHARACTERSHEET || viewType == ViewType.THOUGHTCABINET))
                {
                    context = Loc.Get(skillPoints == 1 ? "SkillPointOne" : "SkillPointsMany", skillPoints);
                }

                if (viewType == ViewType.CHARACTERSHEET)
                {
                    string xp = Loc.Get("ExperienceAndLevel", player.XpAmount, player.Level);
                    context = string.IsNullOrEmpty(context) ? xp : $"{context} {xp}";
                }

                if (viewType == ViewType.INVENTORY)
                {
                    int items = CountCarriedItems();
                    if (items >= 0)
                    {
                        string count = Loc.Get(items == 1 ? "ItemCountOne" : "ItemCountMany", items);
                        context = string.IsNullOrEmpty(count) ? context : count;
                    }
                }

                return context;
            }
            catch
            {
                return "";
            }
        }
    }
}
