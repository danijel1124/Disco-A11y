using MelonLoader;
using UnityEngine;
using AccessibilityMod.Settings;

namespace AccessibilityMod.UI
{
    /// <summary>
    /// Says when a conversation is over.
    ///
    /// The end of a dialogue is obvious on screen - the text panel disappears and you are
    /// standing in the world again - and completely invisible without it: the reading just
    /// stops, and there is no way to tell "the game is waiting for me to pick an option"
    /// apart from "the conversation ended and I can walk away". Navigation silently
    /// unlocks at that moment too, so the announcement is also the cue that the keys work
    /// again.
    ///
    /// Keyed off the dialogue system's own global signal, so it holds for every
    /// conversation in the game.
    /// </summary>
    public static class DialogEndAnnouncer
    {
        private const float POLL_INTERVAL = 0.2f;

        private static float lastPoll;
        private static bool wasActive;

        public static void Update()
        {
            if (Time.unscaledTime - lastPoll < POLL_INTERVAL) return;
            lastPoll = Time.unscaledTime;

            bool active = DialogStateManager.IsDialogUiActive;

            if (wasActive && !active)
            {
                // Queued, so it lands after the last spoken line rather than cutting it off:
                // the final line of a conversation is usually the one worth hearing.
                TolkScreenReader.Instance.Speak(
                    Loc.Get("DialogEnded"), false, AnnouncementCategory.Queueable);
                MelonLogger.Msg("[DIALOG] Conversation ended");
            }

            wasActive = active;
        }
    }
}
