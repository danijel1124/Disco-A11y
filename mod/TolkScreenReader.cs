using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AccessibilityMod.Settings;
using AccessibilityMod.Audio;
using MelonLoader;

namespace AccessibilityMod
{
    /// <summary>
    /// Defines how an announcement should be handled relative to voice audio playback
    /// </summary>
    public enum AnnouncementCategory
    {
        /// <summary>
        /// Announcement speaks immediately, regardless of voice audio playback
        /// Used for navigation, UI selection, and other interactive feedback
        /// </summary>
        Immediate,

        /// <summary>
        /// Announcement is queued during voice audio playback and spoken after audio finishes
        /// Used for dialogue text, notifications, and other content that can wait
        /// </summary>
        Queueable
    }
    /// <summary>
    /// Wrapper around the Tolk screen reader integration library. Public so companion
    /// mods (the AI dev bridge) can observe speech output.
    /// </summary>
    public class TolkScreenReader
    {
        private static TolkScreenReader instance;
        private bool isInitialized = false;
        private bool suppressAnnouncements = false;
        private bool globalInterruptEnabled = false;

        public static TolkScreenReader Instance
        {
            get
            {
                if (instance == null)
                    instance = new TolkScreenReader();
                return instance;
            }
        }

        public bool IsInitialized => isInitialized;

        public bool Initialize()
        {
            try
            {
                Tolk.TrySAPI(true);  // Allow SAPI as fallback
                Tolk.PreferSAPI(false);  // Prefer real screen readers

                Tolk.Load();
                isInitialized = Tolk.IsLoaded();

                // Load settings after initialization
                if (isInitialized)
                {
                    globalInterruptEnabled = AccessibilityPreferences.GetSpeechInterrupt();
                    MelonLogger.Msg($"[TOLK] Speech interrupt loaded from preferences: {globalInterruptEnabled}");
                }

                return isInitialized;
            }
            catch (Exception)
            {
                isInitialized = false;
                return false;
            }
        }

        public string DetectScreenReader()
        {
            if (!isInitialized) return null;
            return Tolk.DetectScreenReader();
        }

        public bool HasSpeech()
        {
            if (!isInitialized) return false;
            return Tolk.HasSpeech();
        }

        public bool HasBraille()
        {
            if (!isInitialized) return false;
            return Tolk.HasBraille();
        }

        private string StripHtmlTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Remove Unity color tags while preserving content
            // This removes <color=...> and </color> tags but keeps the text inside
            text = Regex.Replace(text, @"<color[^>]*>", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</color>", "", RegexOptions.IgnoreCase);
            
            // Remove other common Unity/HTML tags while preserving content
            text = Regex.Replace(text, @"</?[^>]+>", "");
            
            return text;
        }

        public bool Speak(string text, bool interrupt = false, AnnouncementCategory category = AnnouncementCategory.Immediate, Audio.AnnouncementSource source = Audio.AnnouncementSource.Other)
        {
            return SendSpeech(text, interrupt, true, category, source);
        }

        /// <summary>
        /// Speaks with a real QUEUE guarantee: never interrupts, not even when the
        /// player's global speech-interrupt setting (F11, persisted) is on. Speak(text,
        /// interrupt: false) does NOT give that guarantee - SendSpeech promotes queued
        /// lines to interrupting under the global setting, so a follow-up line beheaded
        /// the long announcement it was meant to trail (PR review finding 3: the
        /// research-result read was never heard past ~0 ms). Use this for lines whose
        /// entire point is to come AFTER something else.
        /// </summary>
        public bool SpeakNeverInterrupt(string text, AnnouncementCategory category = AnnouncementCategory.Immediate, Audio.AnnouncementSource source = Audio.AnnouncementSource.Other)
        {
            return SendSpeech(text, false, true, category, source, allowGlobalInterrupt: false);
        }

        /// <summary>
        /// Fires once per line at the moment it actually goes to the screen reader - the
        /// single source of truth for "what did the player hear?", shared by the transcript,
        /// the dev bridge and any debugging tool.
        ///
        /// Listening on Speak() instead is a trap that already cost a debugging session: a
        /// queued announcement passes through Speak twice (enqueue, then play), so a
        /// listener there sees every queued line twice and reports duplicate speech that
        /// the player never heard.
        /// </summary>
        public static event Action<string> Spoken;

        private static void RaiseSpoken(string text)
        {
            try
            {
                Spoken?.Invoke(text);
            }
            catch (Exception ex)
            {
                // A misbehaving listener must never take the speech down with it.
                MelonLogger.Warning($"[TOLK] Spoken listener failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Records a line the player heard through some OTHER channel - the separate SAPI voice
        /// that orb text uses - so the transcript, the speech log and the dev bridge still see
        /// it, without sending the audio through the screen reader a second time. The debugger
        /// exists to catch exactly the ambient-speech bugs orb text produces; those must not
        /// vanish from the record just because they play on a different voice.
        /// </summary>
        public void RecordExternalSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = StripHtmlTags(text);
            SpeechLog.Write(text);
            RaiseSpoken(text);
        }

        // Enable to log Unicode code points of text sent to the screen reader
        public bool DiagnosticLogging = false;

        private void LogSpeechDiagnostics(string text)
        {
            if (!DiagnosticLogging || string.IsNullOrEmpty(text)) return;

            var sb = new StringBuilder();
            sb.Append($"[SPEECH-DIAG] Len={text.Length} Text=\"{text}\" FirstCodePoints=[");
            int limit = Math.Min(text.Length, 20);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"U+{(int)text[i]:X4}");
            }
            if (text.Length > limit) sb.Append(", ...");
            sb.Append("]");

            // Check if any characters are in Arabic Unicode blocks (U+0600-U+06FF, U+0750-U+077F, U+FB50-U+FDFF, U+FE70-U+FEFF)
            bool hasArabic = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F) ||
                    (c >= 0xFB50 && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF))
                {
                    hasArabic = true;
                    break;
                }
            }
            sb.Append(hasArabic ? " [ARABIC DETECTED]" : " [NO ARABIC CHARS]");

            MelonLogger.Msg(sb.ToString());
        }

        private bool SendSpeech(string text, bool interrupt, bool respectSuppression, AnnouncementCategory category = AnnouncementCategory.Immediate, Audio.AnnouncementSource source = Audio.AnnouncementSource.Other, bool allowGlobalInterrupt = true)
        {
            if (!isInitialized || string.IsNullOrEmpty(text)) return false;
            if (respectSuppression && suppressAnnouncements) return false;

            text = StripHtmlTags(text);
            LogSpeechDiagnostics(text);

            // Route based on announcement category
            if (category == AnnouncementCategory.Queueable)
            {
                // Queue the announcement to be spoken when voice audio is not playing
                AudioAwareAnnouncementManager.Instance.QueueAnnouncement(text, interrupt, source);
                return true;
            }
            else
            {
                // Logged here rather than above, because a queued announcement passes
                // through this method twice - once to be queued, once to be spoken - and
                // a transcript that says everything twice is worse than none. This is also
                // the moment the player actually hears it, which is what the timestamps
                // are supposed to mean.
                SpeechLog.Write(text);
                RaiseSpoken(text);

                // Immediate announcement - speak right away.
                // The global interrupt setting promotes queued lines to interrupting -
                // EXCEPT for callers that need a genuine queue guarantee (a follow-up
                // line must never behead the announcement it trails, see
                // SpeakNeverInterrupt).
                bool effectiveInterrupt = interrupt || (allowGlobalInterrupt && globalInterruptEnabled);
                return Tolk.Output(text, effectiveInterrupt);
            }
        }

        public void ToggleGlobalInterrupt()
        {
            globalInterruptEnabled = !globalInterruptEnabled;
            string status = globalInterruptEnabled ? "enabled" : "disabled";
            // Always interrupt this announcement so user gets immediate feedback
            Speak($"Speech interrupt {status}", true);

            // Save the new setting
            AccessibilityPreferences.SetSpeechInterrupt(globalInterruptEnabled);
            MelonLogger.Msg($"[TOLK] Speech interrupt {status} and saved");
        }

        public bool IsGlobalInterruptEnabled()
        {
            return globalInterruptEnabled;
        }

        public void SuppressAnnouncements(bool suppress)
        {
            suppressAnnouncements = suppress;
        }

        public bool Output(string text, bool interrupt = false)
        {
            return SendSpeech(text, interrupt, false);
        }

        public bool Braille(string text)
        {
            if (!isInitialized || string.IsNullOrEmpty(text)) return false;
            text = StripHtmlTags(text);
            return Tolk.Braille(text);
        }

        public bool IsSpeaking()
        {
            if (!isInitialized) return false;
            return Tolk.IsSpeaking();
        }

        public bool Silence()
        {
            if (!isInitialized) return false;
            return Tolk.Silence();
        }

        public void Shutdown()
        {
            if (isInitialized)
            {
                Tolk.Unload();
                isInitialized = false;
            }
        }

        public void Cleanup()
        {
            Shutdown();
        }
    }

    // Official Tolk .NET wrapper class
    public sealed class Tolk 
    {
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        private static extern void Tolk_Load();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_IsLoaded();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI(
            [MarshalAs(UnmanagedType.I1)]bool trySAPI);
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        private static extern void Tolk_PreferSAPI(
            [MarshalAs(UnmanagedType.I1)]bool preferSAPI);
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_HasSpeech();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_HasBraille();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Output(
            [MarshalAs(UnmanagedType.LPWStr)]String str,
            [MarshalAs(UnmanagedType.I1)]bool interrupt);
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Speak(
            [MarshalAs(UnmanagedType.LPWStr)]String str,
            [MarshalAs(UnmanagedType.I1)]bool interrupt);
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Braille(
            [MarshalAs(UnmanagedType.LPWStr)]String str);
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_IsSpeaking();
        [DllImport("Tolk.dll", CharSet=CharSet.Unicode, CallingConvention=CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Silence();

        // Prevent construction
        private Tolk() {}

        public static void Load() { Tolk_Load(); }
        public static bool IsLoaded() { return Tolk_IsLoaded(); }
        public static void Unload() { Tolk_Unload(); }
        public static void TrySAPI(bool trySAPI) { Tolk_TrySAPI(trySAPI); }
        public static void PreferSAPI(bool preferSAPI) { Tolk_PreferSAPI(preferSAPI); }
        // Prevent the marshaller from freeing the unmanaged string
        public static String DetectScreenReader() { return Marshal.PtrToStringUni(Tolk_DetectScreenReader()); }
        public static bool HasSpeech() { return Tolk_HasSpeech(); }
        public static bool HasBraille() { return Tolk_HasBraille(); }
        public static bool Output(String str, bool interrupt = false) { return Tolk_Output(str, interrupt); }
        public static bool Speak(String str, bool interrupt = false) { return Tolk_Speak(str, interrupt); }
        public static bool Braille(String str) { return Tolk_Braille(str); }
        public static bool IsSpeaking() { return Tolk_IsSpeaking(); }
        public static bool Silence() { return Tolk_Silence(); }
    }
}