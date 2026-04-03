using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;

namespace AccessibilityMod.Utils
{
    /// <summary>
    /// Handles Right-to-Left text correction for screen reader output.
    ///
    /// The game uses two different text pipelines for Arabic:
    /// 1. I2 Localization applies Arabic shaping (presentation forms) AND reverses the string
    ///    for visual display in TMP — these strings need reversing back to logical order.
    /// 2. Some components use TMP's native RTL mode, which stores text in logical order
    ///    with presentation forms — these strings are already correct.
    ///
    /// We detect which pipeline was used via three signals: leading punctuation position,
    /// the first Arabic Presentation Form character type, and word-boundary positional
    /// form analysis. See IsVisualOrder() for details.
    /// </summary>
    public static class RTLHelper
    {
        private static bool? _cachedIsRTL = null;
        private static string _cachedLanguage = null;

        /// <summary>
        /// Check if the current game language is RTL, using the game's own localization API.
        /// Result is cached until ClearCache() is called.
        /// </summary>
        public static bool IsCurrentLanguageRTL()
        {
            if (_cachedIsRTL.HasValue)
                return _cachedIsRTL.Value;

            try
            {
                bool isRTL = Il2CppLocalizationCustomSystem.LocalizationManager.IsRightToLeftText();
                _cachedLanguage = Il2CppLocalizationCustomSystem.LocalizationManager.GetCurrentLanguageName();
                _cachedIsRTL = isRTL;
                MelonLogger.Msg($"[RTL] Language: {_cachedLanguage}, RTL: {isRTL}");
                return isRTL;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[RTL] Could not detect RTL status: {ex.Message}");
                _cachedIsRTL = false;
                return false;
            }
        }

        /// <summary>
        /// Clear the cached RTL state. Call this when the language changes.
        /// </summary>
        public static void ClearCache()
        {
            _cachedIsRTL = null;
            _cachedLanguage = null;
        }

        /// <summary>
        /// Fix RTL text for screen reader output. Automatically detects whether the text
        /// is in visual (I2-reversed) or logical order by examining Arabic presentation
        /// form characters, and only reverses text that needs it.
        /// </summary>
        public static string FixForScreenReader(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsCurrentLanguageRTL())
                return text;

            // Strip Unity rich text tags before RTL processing. Tags like
            // <color=#FFFFFF> would be reversed into gibberish (>FFFFFF#=roloc<)
            // that the later StripHtmlTags in TolkScreenReader can't match.
            text = Regex.Replace(text, @"<[^>]+>", "");

            if (!ContainsRTLCharacters(text))
                return text;

            // Detect text direction from content.
            // I2-reversed text has a FINAL presentation form as its first shaped char.
            // Logical-order text has an INITIAL or ISOLATED form first.
            if (!IsVisualOrder(text))
            {
                if (UI.TextExtractor.DiagnosticLogging)
                    MelonLogger.Msg($"[RTL-FIX] Already logical: \"{(text.Length > 40 ? text.Substring(0, 40) + "..." : text)}\"");
                return text;
            }

            // Determine reversal strategy. I2 uses two different reversal modes:
            // - Pure Arabic text: full character-by-character reversal (numbers reversed too)
            // - Mixed LTR+Arabic text: bidi-aware reversal (LTR runs preserved)
            // When Latin letters precede Arabic, I2 preserved LTR runs, so we must
            // re-reverse them after our full reversal to keep them correct.
            bool preserveLTR = HasLatinBeforeArabic(text);

            // Normalize line endings before splitting — the game mixes \r\n and \n,
            // and stray \r can cause lines to merge or split incorrectly.
            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // Reverse each line independently to preserve line order.
            // A full-string reverse would flip line order (Line1\nLine2 → Line2\nLine1).
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (ContainsRTLCharacters(lines[i]))
                    lines[i] = preserveLTR ? ReverseLinePreserveLTR(lines[i]) : ReverseLine(lines[i]);
            }
            string result = string.Join("\n", lines);

            if (UI.TextExtractor.DiagnosticLogging)
            {
                string before = text.Length > 30 ? text.Substring(0, 30) + "..." : text;
                string after = result.Length > 30 ? result.Substring(0, 30) + "..." : result;
                MelonLogger.Msg($"[RTL-FIX] Reversed: \"{before}\" → \"{after}\"");
            }

            return result;
        }

        /// <summary>
        /// Detect if Arabic text is in visual (I2-reversed) order using five signals:
        ///
        /// 1. Punctuation check: Sentence-ending punctuation (. ! ? : ;) at string start
        ///    indicates reversal from the end of logical text.
        ///
        /// 2. Presentation form check: First Arabic PF is a FINAL form (last logical char
        ///    moved to first position). INITIAL/ISOLATED = logical order.
        ///
        /// 3. Word-boundary check: Post-space letters in FINAL/MEDIAL form indicate
        ///    reversed word order (logical Arabic has INITIAL/ISOLATED after spaces).
        ///
        /// 4. Word-final-only letters: Ta Marbuta (U+0629) or Alef Maqsura (U+0649)
        ///    at string start — impossible in logical Arabic.
        ///
        /// 5. I2 partial shaping: Lam-Alef ligature as ONLY PF with connecting letters
        ///    in base form and zero other PF chars — unique to I2's ligature-only shaping.
        /// </summary>
        private static bool IsVisualOrder(string text)
        {
            // Signal 1: Check if text starts with sentence-ending punctuation followed
            // by Arabic. Skip leading whitespace and quotation marks first.
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;

                // Skip quotation marks — these are directionally neutral and can wrap
                // either logical or visual text
                if (c == '"' || c == '\'' || c == '\u201C' || c == '\u201D' || // " " ' "
                    c == '\u2018' || c == '\u2019' || c == '\u00AB' || c == '\u00BB') // ' ' « »
                    continue;

                // If the first non-whitespace, non-quote char is sentence-ending punctuation
                // and there are Arabic chars after it, this is visual order
                if (c == '.' || c == '!' || c == '?' || c == '\u061F' || // U+061F = Arabic question mark
                    c == ':' || c == '\u061B') // U+061B = Arabic semicolon
                    // Note: closing brackets (] ) }) are NOT included here because TMP's
                    // native RTL mode uses bracket mirroring — ] in the string displays as [
                    // visually. A ] at string start is normal for RTL logical text.
                {
                    if (ContainsRTLCharacters(text.Substring(i + 1)))
                        return true;
                }
                break; // Only check the first significant character
            }

            // Signal 2: Check the first Arabic Presentation Form character
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                // Only check Arabic Presentation Forms-B (where positional forms live)
                if (c >= 0xFE70 && c <= 0xFEFC)
                {
                    if (IsArabicFinalForm(c))
                        return true;
                    break; // First form found but not final — check signal 3
                }
            }

            // Signal 3: Check word boundaries. In logical Arabic, the first letter of each
            // word (after a space) is always in INITIAL or ISOLATED form. In I2-reversed text,
            // these positions contain FINAL or MEDIAL forms because the word order is inverted.
            bool afterSpace = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    afterSpace = true;
                    continue;
                }
                if (afterSpace && c >= 0xFE70 && c <= 0xFEFC)
                {
                    if (IsArabicFinalForm(c) || IsArabicMedialForm(c))
                        return true;
                    afterSpace = false;
                }
                else
                {
                    afterSpace = false;
                }
            }

            // Signal 4: Word-final-only characters at string start.
            // Ta Marbuta (U+0629) and Alef Maqsura (U+0649) ONLY appear at the end of
            // Arabic words — never at the start. Finding one as the first non-whitespace
            // character is linguistically impossible in logical order.
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) continue;
                if (c == '\u0629' || c == '\u0649') // Ta Marbuta or Alef Maqsura
                    return true;
                break;
            }

            // Signal 5: I2 partial shaping — only Lam-Alef ligatures shaped.
            // I2 sometimes partially shapes text, creating only Lam-Alef ligatures
            // (U+FEF5-FEFC) while leaving ALL other characters as base Arabic. TMP's
            // shaping converts connecting letters in connected positions to PF forms,
            // but may leave connecting letters in isolated position (after non-connecting
            // chars) as base — so the presence of connecting letters in base form alone
            // is not sufficient. We require all three conditions:
            // 1. Lam-Alef ligature present (I2 shaped at least the ligature)
            // 2. No other PF characters (I2 didn't shape anything else)
            // 3. Connecting letter in base form (I2 left connected letters unshaped)
            bool hasLamAlef = false;
            bool hasOtherPF = false;
            bool hasConnectingInBase = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 0xFEF5 && c <= 0xFEFC)
                    hasLamAlef = true;
                else if (c >= 0xFE70 && c <= 0xFEF4)
                    hasOtherPF = true;
                if (IsConnectingArabicLetter(c))
                    hasConnectingInBase = true;
            }
            if (hasLamAlef && hasConnectingInBase && !hasOtherPF)
                return true;

            // No signals found — default to not reversing.
            return false;
        }

        /// <summary>
        /// Check if a character is a base Arabic connecting letter (one that joins to the
        /// following letter). In properly shaped text (TMP native RTL), these always appear
        /// as Presentation Form variants, never as base Arabic.
        /// </summary>
        private static bool IsConnectingArabicLetter(char c)
        {
            switch (c)
            {
                case '\u0626': // Ya-hamza
                case '\u0628': // Ba
                case '\u062A': // Ta
                case '\u062B': // Tha
                case '\u062C': // Jim
                case '\u062D': // Ha (haa)
                case '\u062E': // Kha
                case '\u0633': // Sin
                case '\u0634': // Shin
                case '\u0635': // Sad
                case '\u0636': // Dad
                case '\u0637': // Tah
                case '\u0638': // Zah
                case '\u0639': // Ain
                case '\u063A': // Ghain
                case '\u0641': // Fa
                case '\u0642': // Qaf
                case '\u0643': // Kaf
                case '\u0644': // Lam
                case '\u0645': // Meem
                case '\u0646': // Noon
                case '\u0647': // Ha (ha)
                case '\u064A': // Ya
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if a character is an Arabic Presentation Forms-B FINAL positional form.
        /// These are the forms used at the END of a word in logical reading order.
        /// If one appears at the START of a string, the text was reversed for visual display.
        ///
        /// Reference: Unicode Standard, Arabic Presentation Forms-B (U+FE70-U+FEFF)
        /// Each Arabic letter has forms organized as: isolated(+0), final(+1), initial(+2), medial(+3)
        /// Letters with only 2 forms have: isolated(+0), final(+1)
        /// </summary>
        private static bool IsArabicFinalForm(char c)
        {
            switch (c)
            {
                // Alif variants: madda-final, hamza-above-final, waw-hamza-final, hamza-below-final
                case '\uFE82': case '\uFE84': case '\uFE86': case '\uFE88':
                // Yaa-hamza final, Alif final, Baa final, Taa-marbuta final
                case '\uFE8A': case '\uFE8E': case '\uFE90': case '\uFE94':
                // Taa final, Thaa final, Jeem final, Haa final
                case '\uFE96': case '\uFE9A': case '\uFE9E': case '\uFEA2':
                // Khaa final, Dal final, Thal final, Ra final
                case '\uFEA6': case '\uFEAA': case '\uFEAC': case '\uFEAE':
                // Zain final, Seen final, Sheen final, Sad final
                case '\uFEB0': case '\uFEB2': case '\uFEB6': case '\uFEBA':
                // Dad final, Tah final, Zah final, Ain final
                case '\uFEBE': case '\uFEC2': case '\uFEC6': case '\uFECA':
                // Ghain final, Fa final, Qaf final, Kaf final
                case '\uFECE': case '\uFED2': case '\uFED6': case '\uFEDA':
                // Lam final, Meem final, Noon final, Ha final
                case '\uFEDE': case '\uFEE2': case '\uFEE6': case '\uFEEA':
                // Waw final, Alif-maksura final, Ya final
                case '\uFEEE': case '\uFEF0': case '\uFEF2':
                // Lam-Alif ligature finals (madda, hamza-above, hamza-below, plain)
                case '\uFEF6': case '\uFEF8': case '\uFEFA': case '\uFEFC':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if a character is an Arabic Presentation Forms-B MEDIAL positional form.
        /// These appear in the MIDDLE of a word in logical reading order.
        /// Like final forms, they should never appear at the start of a word.
        ///
        /// Only letters with 4 forms have medial variants.
        /// Pattern: isolated(+0), final(+1), initial(+2), medial(+3)
        /// </summary>
        private static bool IsArabicMedialForm(char c)
        {
            switch (c)
            {
                // Yaa-hamza medial, Baa medial, Taa-marbuta medial (rare), Taa medial
                case '\uFE8C': case '\uFE92': case '\uFE98':
                // Thaa medial, Jeem medial, Haa medial, Khaa medial
                case '\uFE9C': case '\uFEA0': case '\uFEA4': case '\uFEA8':
                // Seen medial, Sheen medial, Sad medial, Dad medial
                case '\uFEB4': case '\uFEB8': case '\uFEBC': case '\uFEC0':
                // Tah medial, Zah medial, Ain medial, Ghain medial
                case '\uFEC4': case '\uFEC8': case '\uFECC': case '\uFED0':
                // Fa medial, Qaf medial, Kaf medial, Lam medial
                case '\uFED4': case '\uFED8': case '\uFEDC': case '\uFEE0':
                // Meem medial, Noon medial, Ha medial, Ya medial
                case '\uFEE4': case '\uFEE8': case '\uFEEC': case '\uFEF4':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if a string contains any RTL characters.
        /// </summary>
        private static bool ContainsRTLCharacters(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (IsRTLChar(text[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Reverse a single line of text, correctly handling surrogate pairs
        /// and combining characters.
        ///
        /// I2 Localization reverses the entire string character-by-character for
        /// visual RTL display, including embedded numbers. A simple reversal back
        /// to logical order automatically restores both Arabic text and number
        /// sequences to their correct reading order.
        /// </summary>
        private static string ReverseLine(string text)
        {
            var elements = StringInfo.GetTextElementEnumerator(text);
            var textElements = new System.Collections.Generic.List<string>();
            while (elements.MoveNext())
            {
                textElements.Add(elements.GetTextElement());
            }
            textElements.Reverse();

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < textElements.Count; i++)
            {
                sb.Append(textElements[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Reverse a line while preserving LTR runs (Latin letters, digits).
        /// Used when I2 performed bidi-aware reversal (Arabic reversed, LTR preserved).
        /// After our full reversal, LTR runs need to be re-reversed to stay correct.
        /// </summary>
        private static string ReverseLinePreserveLTR(string text)
        {
            var elements = StringInfo.GetTextElementEnumerator(text);
            var textElements = new System.Collections.Generic.List<string>();
            while (elements.MoveNext())
            {
                textElements.Add(elements.GetTextElement());
            }
            textElements.Reverse();

            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < textElements.Count)
            {
                if (IsLTRElement(textElements[i]))
                {
                    int start = i;
                    i++;
                    while (i < textElements.Count)
                    {
                        if (IsLTRElement(textElements[i]))
                        {
                            i++;
                        }
                        else if (IsLTRConnector(textElements[i]) &&
                                 i + 1 < textElements.Count &&
                                 IsLTRElement(textElements[i + 1]))
                        {
                            i++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    // Re-reverse the LTR run
                    for (int j = i - 1; j >= start; j--)
                        sb.Append(textElements[j]);
                }
                else
                {
                    sb.Append(textElements[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Check if Latin letters appear before the first Arabic character.
        /// This indicates I2 used bidi-aware reversal (preserving LTR runs)
        /// rather than full character-by-character reversal.
        /// </summary>
        private static bool HasLatinBeforeArabic(string text)
        {
            bool seenLatin = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    seenLatin = true;
                    continue;
                }
                if (IsRTLChar(c))
                    return seenLatin;
            }
            return false;
        }

        /// <summary>
        /// Check if a text element is an LTR character (Latin letter or digit).
        /// </summary>
        private static bool IsLTRElement(string element)
        {
            if (element.Length != 1) return false;
            char c = element[0];
            return (c >= '0' && c <= '9') ||
                   (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   (c >= '\u0660' && c <= '\u0669') || // Arabic-Indic digits
                   (c >= '\u06F0' && c <= '\u06F9');   // Extended Arabic-Indic digits
        }

        /// <summary>
        /// Check if a text element connects parts of an LTR run
        /// (hyphens in dates, periods in decimals, etc.).
        /// </summary>
        private static bool IsLTRConnector(string element)
        {
            if (element.Length != 1) return false;
            char c = element[0];
            return c == '-' || c == '.' || c == ':' || c == '/';
        }

        /// <summary>
        /// Check if a character is in an RTL Unicode block (Arabic, Hebrew, etc.)
        /// </summary>
        private static bool IsRTLChar(char c)
        {
            return (c >= 0x0590 && c <= 0x05FF) ||  // Hebrew
                   (c >= 0x0600 && c <= 0x06FF) ||  // Arabic
                   (c >= 0x0750 && c <= 0x077F) ||  // Arabic Supplement
                   (c >= 0x08A0 && c <= 0x08FF) ||  // Arabic Extended-A
                   (c >= 0xFB50 && c <= 0xFDFF) ||  // Arabic Presentation Forms-A
                   (c >= 0xFE70 && c <= 0xFEFF);    // Arabic Presentation Forms-B
        }
    }
}
