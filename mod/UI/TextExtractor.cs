using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using AccessibilityMod.Utils;
using MelonLoader;

namespace AccessibilityMod.UI
{
    /// <summary>
    /// Utility class for extracting text content from UI GameObjects
    /// </summary>
    public static class TextExtractor
    {
        // Enable via MelonLoader console or set to true to diagnose encoding issues
        public static bool DiagnosticLogging = false;

        /// <summary>
        /// Log Unicode code points for the first N characters of a string, to diagnose encoding issues.
        /// Enable by setting TextExtractor.DiagnosticLogging = true.
        /// </summary>
        private static void LogStringDiagnostics(string text, string source)
        {
            if (!DiagnosticLogging || string.IsNullOrEmpty(text)) return;

            var sb = new StringBuilder();
            sb.Append($"[TEXT-DIAG] Source={source} Len={text.Length} Text=\"{text}\" CodePoints=[");
            int limit = Math.Min(text.Length, 20);
            for (int i = 0; i < limit; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"U+{(int)text[i]:X4}");
            }
            if (text.Length > limit) sb.Append(", ...");
            sb.Append("]");

            // Flag if text contains characters that suggest encoding corruption
            bool hasNonBmpSubstitution = false;
            for (int i = 0; i < text.Length; i++)
            {
                // Check for common mojibake patterns: Latin chars where Arabic expected
                if (text[i] >= 0x00C0 && text[i] <= 0x00FF) hasNonBmpSubstitution = true;
            }
            if (hasNonBmpSubstitution)
                sb.Append(" WARNING: Contains Latin Extended chars (possible UTF-8 misinterpreted as Latin-1)");

            MelonLogger.Msg(sb.ToString());
        }

        /// <summary>
        /// Extract the best text content from a GameObject, checking multiple text component types
        /// </summary>
        public static string ExtractBestTextContent(GameObject uiObject)
        {
            try
            {
                if (uiObject == null) return null;

                string result = null;

                // Try direct text components first
                var textComponent = uiObject.GetComponent<Text>();
                if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                {
                    result = textComponent.text.Trim();
                    LogStringDiagnostics(result, $"Text@{uiObject.name}");
                }
                else
                {
                    var tmpText = uiObject.GetComponent<TextMeshProUGUI>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        result = tmpText.text.Trim();
                        LogStringDiagnostics(result, $"TMP_UGUI@{uiObject.name}");
                    }
                    else
                    {
                        var tmpTextPro = uiObject.GetComponent<TextMeshPro>();
                        if (tmpTextPro != null && !string.IsNullOrEmpty(tmpTextPro.text))
                        {
                            result = tmpTextPro.text.Trim();
                            LogStringDiagnostics(result, $"TMP_Pro@{uiObject.name}");
                        }
                    }
                }

                // Try child text components if direct ones failed
                if (result == null)
                {
                    var childText = uiObject.GetComponentInChildren<Text>();
                    if (childText != null && !string.IsNullOrEmpty(childText.text))
                    {
                        result = childText.text.Trim();
                        LogStringDiagnostics(result, $"ChildText@{uiObject.name}");
                    }
                    else
                    {
                        var childTMP = uiObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (childTMP != null && !string.IsNullOrEmpty(childTMP.text))
                        {
                            result = childTMP.text.Trim();
                            LogStringDiagnostics(result, $"ChildTMP@{uiObject.name}");
                        }
                        else
                        {
                            var childTMPPro = uiObject.GetComponentInChildren<TextMeshPro>();
                            if (childTMPPro != null && !string.IsNullOrEmpty(childTMPPro.text))
                            {
                                result = childTMPPro.text.Trim();
                                LogStringDiagnostics(result, $"ChildTMPPro@{uiObject.name}");
                            }
                        }
                    }
                }

                // Some of the game's buttons carry no text at all: they render their label
                // as a per-language sprite (I2LocalizeButton swaps the image, see the
                // "BEGINNEN" button that starts the intro). Those were unreadable by any
                // screen reader. They do carry the localization term they were rendered
                // from, so ask the game's own localization system what that term says.
                if (string.IsNullOrEmpty(result))
                {
                    result = ExtractLocalizedTerm(uiObject);
                }

                // Fix RTL text that was reversed by I2 Localization for visual display.
                // FixForScreenReader auto-detects whether text needs reversal by examining
                // Arabic presentation form characters — no external flags needed.
                return RTLHelper.FixForScreenReader(result);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error extracting text content: {ex}");
                return null;
            }
        }

        /// <summary>
        /// The label of an element whose text exists only as a localized image, resolved
        /// through the game's own localization system. Returns null when the element has
        /// no localization component, which is the normal case.
        /// </summary>
        private static string ExtractLocalizedTerm(GameObject uiObject)
        {
            try
            {
                var localize = uiObject.GetComponent<Il2CppI2.Loc.Localize>()
                               ?? uiObject.GetComponentInChildren<Il2CppI2.Loc.Localize>();
                if (localize == null) return null;

                string term = localize.Term;
                if (string.IsNullOrEmpty(term)) return null;

                string translated = Il2CppI2.Loc.LocalizationManager.GetTranslation(term);
                if (!string.IsNullOrWhiteSpace(translated) && translated != term) return translated.Trim();

                // These terms are sprite names, not text keys ("button-begin-de"), so the
                // localization table has nothing to say about them. Strip the decoration
                // down to the meaning ("begin") and let our own table name it properly.
                return NameFromSpriteTerm(term);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TEXT] Could not resolve localized term: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// A speakable label from a localized sprite name: "Buttons/button-begin-de" becomes
        /// "begin", which our own table then names in the player's language. Terms we have
        /// no name for at least come out as readable words instead of a slug.
        /// </summary>
        private static string NameFromSpriteTerm(string term)
        {
            int slash = term.LastIndexOf('/');
            string slug = slash >= 0 ? term.Substring(slash + 1) : term;

            var parts = slug.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // The sprite exists once per language, so the term carries a language suffix
            // ("...-de") that says nothing about what the button does.
            if (parts.Count > 1 && parts[parts.Count - 1].Length == 2)
                parts.RemoveAt(parts.Count - 1);

            if (parts.Count > 1 && (parts[0] == "button" || parts[0] == "btn"))
                parts.RemoveAt(0);

            if (parts.Count == 0) return term;

            string key = "UITerm_" + string.Join("-", parts);
            if (Settings.Loc.Has(key)) return Settings.Loc.Get(key);

            string words = string.Join(" ", parts);
            return char.ToUpperInvariant(words[0]) + words.Substring(1);
        }

        /// <summary>
        /// Search for descriptive text within a GameObject hierarchy, with filtering for likely descriptions
        /// </summary>
        public static string SearchForDescriptiveText(GameObject parent, Func<string, bool> textValidator, string contextName = "")
        {
            try
            {
                if (parent == null) return null;

                // Get all text components in this object and its children
                var allTextComponents = new List<Component>();
                
                // Add TextMeshProUGUI components
                var tmpComponents = parent.GetComponentsInChildren<TextMeshProUGUI>();
                if (tmpComponents != null)
                {
                    allTextComponents.AddRange(tmpComponents);
                }
                
                // Add regular Text components  
                var textComponents = parent.GetComponentsInChildren<Text>();
                if (textComponents != null)
                {
                    allTextComponents.AddRange(textComponents);
                }
                
                // Search text components for descriptive content
                foreach (var component in allTextComponents)
                {
                    string text = null;
                    
                    if (component is TextMeshProUGUI tmpText && tmpText != null)
                    {
                        text = tmpText.text;
                    }
                    else if (component is Text regularText && regularText != null)
                    {
                        text = regularText.text;
                    }
                    
                    if (!string.IsNullOrEmpty(text) && textValidator(text))
                    {
                        return RTLHelper.FixForScreenReader(text.Trim());
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error searching for descriptive text in {contextName}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Find displayed text by searching up the hierarchy from a starting object
        /// </summary>
        public static string FindDisplayedDescription(GameObject startObject, Func<string, bool> textValidator, int maxLevels = 4, string contextName = "")
        {
            try
            {
                // Search in the starting object and its broader hierarchy for description text
                var candidates = new List<GameObject> { startObject };
                
                // Add parent objects that might contain the description area
                var current = startObject.transform;
                for (int i = 0; i < maxLevels && current != null; i++)
                {
                    current = current.parent;
                    if (current != null)
                    {
                        candidates.Add(current.gameObject);
                    }
                }
                
                // Search each candidate for descriptive text
                foreach (var candidate in candidates)
                {
                    string description = SearchForDescriptiveText(candidate, textValidator, contextName);
                    if (!string.IsNullOrEmpty(description))
                    {
                        return description;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error finding displayed description for {contextName}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Common text validation for skill descriptions
        /// </summary>
        public static bool IsLikelySkillDescriptionText(string text, string skillName = "")
        {
            if (string.IsNullOrEmpty(text) || text.Length < 20)
                return false;
                
            // Skip if it's just the skill name itself
            if (!string.IsNullOrEmpty(skillName) && text.Trim().Equals(skillName.Replace('_', ' '), StringComparison.OrdinalIgnoreCase))
                return false;
                
            // Skip pure numbers or very short text
            if (text.Trim().Length < 15 || text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))
                return false;
                
            // Skip common UI text that's not descriptions  
            string lowerText = text.ToLower();
            if (lowerText.Contains("select button") || lowerText.Contains("level up") || 
                (lowerText.Contains("point") && lowerText.Length < 25))
                return false;
                
            // Look for description-like content - complete sentences or skill-related keywords
            return lowerText.Contains("skill") || lowerText.Contains("ability") ||
                   lowerText.Contains("helps") || lowerText.Contains("allows") ||
                   lowerText.Contains("used") || lowerText.Contains("affects") ||
                   lowerText.Contains("reasoning") || lowerText.Contains("knowledge") ||
                   lowerText.Contains("social") || lowerText.Contains("physical") ||
                   (text.Contains(".") && text.Split('.').Length > 1); // Multiple sentences
        }

        /// <summary>
        /// Common text validation for archetype descriptions
        /// </summary>
        public static bool IsLikelyArchetypeDescriptionText(string text, string archetypeName = "")
        {
            if (string.IsNullOrEmpty(text) || text.Length < 20)
                return false;
                
            // Skip if it's just the archetype name itself
            if (!string.IsNullOrEmpty(archetypeName) && text.Trim().Equals(archetypeName, StringComparison.OrdinalIgnoreCase))
                return false;
                
            // Skip pure numbers or very short text
            if (text.Trim().Length < 15 || text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))
                return false;
                
            // Skip common UI text that's not descriptions  
            string lowerText = text.ToLower();
            if (lowerText.Contains("button") || lowerText.Contains("select") || 
                lowerText.Contains("click") || (lowerText.Contains("archetype") && lowerText.Length < 30))
                return false;
                
            // Look for description-like content - complete sentences or archetype-related keywords
            return lowerText.Contains("approach") || lowerText.Contains("focuses") ||
                   lowerText.Contains("specializes") || lowerText.Contains("excels") ||
                   lowerText.Contains("strength") || lowerText.Contains("abilities") ||
                   lowerText.Contains("intellect") || lowerText.Contains("empathy") ||
                   lowerText.Contains("physical") || lowerText.Contains("reasoning") ||
                   (text.Contains(".") && text.Split('.').Length > 1); // Multiple sentences
        }

        /// <summary>
        /// Common text validation for stat descriptions
        /// </summary>
        public static bool IsLikelyStatDescriptionText(string text, string statName = "")
        {
            if (string.IsNullOrEmpty(text) || text.Length < 10)
                return false;

            // Skip if it's just the stat name itself
            if (!string.IsNullOrEmpty(statName) && text.Trim().Equals(statName, StringComparison.OrdinalIgnoreCase))
                return false;

            // Skip pure numbers or very short text
            if (text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))
                return false;

            // Skip common UI text that's not descriptions
            string lowerText = text.ToLower();
            if (lowerText.Contains("button") || lowerText.Contains("select") ||
                (lowerText.Contains("point") && lowerText.Length < 20))
                return false;

            // If we have a specific stat name, look for stat-specific descriptions
            if (!string.IsNullOrEmpty(statName))
            {
                string lowerStatName = statName.ToLower();

                // Look for stat-specific keywords
                switch (lowerStatName)
                {
                    case "intellect":
                        return lowerText.Contains("brain power") || lowerText.Contains("smart") ||
                               lowerText.Contains("intelligence") || lowerText.Contains("mental") ||
                               lowerText.Contains("logic") || lowerText.Contains("reasoning");

                    case "psyche":
                        return lowerText.Contains("sensitivity") || lowerText.Contains("emotional") ||
                               lowerText.Contains("empathy") || lowerText.Contains("social") ||
                               lowerText.Contains("feelings") || lowerText.Contains("emotionally intelligent");

                    case "physique":
                        return lowerText.Contains("musculature") || lowerText.Contains("strong") ||
                               lowerText.Contains("strength") || lowerText.Contains("physical") ||
                               lowerText.Contains("muscle") || lowerText.Contains("body");

                    case "motorics":
                        return lowerText.Contains("senses") || lowerText.Contains("agile") ||
                               lowerText.Contains("agility") || lowerText.Contains("coordination") ||
                               lowerText.Contains("dexterity") || lowerText.Contains("motor");
                }
            }

            // General fallback - look for description-like content
            return lowerText.Contains("affects") || lowerText.Contains("determines") ||
                   lowerText.Contains("governs") || lowerText.Contains("influences") ||
                   lowerText.Contains("skills") || lowerText.Contains("abilities") ||
                   (text.Contains(".") && text.Split('.').Length > 1); // Multiple sentences
        }

        /// <summary>
        /// Extract text from a UI component using reflection (for complex components)
        /// </summary>
        public static string ExtractTooltipDescription(object tooltipData)
        {
            try
            {
                if (tooltipData == null) return null;
                
                // Use reflection to look for common description fields
                var type = tooltipData.GetType();
                var fields = type.GetFields();
                var properties = type.GetProperties();
                
                // Look for fields that might contain description
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("description") || 
                        field.Name.ToLower().Contains("text") ||
                        field.Name.ToLower().Contains("content"))
                    {
                        var value = field.GetValue(tooltipData);
                        if (value != null && value is string str && !string.IsNullOrEmpty(str))
                        {
                            return str.Trim();
                        }
                    }
                }
                
                // Look for properties that might contain description
                foreach (var prop in properties)
                {
                    if (prop.CanRead && (prop.Name.ToLower().Contains("description") || 
                        prop.Name.ToLower().Contains("text") ||
                        prop.Name.ToLower().Contains("content")))
                    {
                        try
                        {
                            var value = prop.GetValue(tooltipData);
                            if (value != null && value is string str && !string.IsNullOrEmpty(str))
                            {
                                return str.Trim();
                            }
                        }
                        catch { /* Property might not be accessible */ }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error extracting tooltip description: {ex.Message}");
                return null;
            }
        }
    }
}