using System;
using System.Linq;
using MelonLoader;
using AccessibilityMod.Settings;
using Il2CppSunshine.Metric;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Keyboard access to the healing plus buttons (bug #56). The game renders a plus
    /// button on the health bar and one on the morale bar; clicking one consumes a
    /// healing charge from a carried healing item. Both buttons are mouse-only: no game
    /// key reaches them, so a keyboard player could carry medicine and never use it.
    ///
    /// Ctrl+H heals health (Endurance pool), Shift+H heals morale (Volition pool) - the
    /// bindings are remappable; these are the defaults. (Two earlier bindings were
    /// abandoned: KeyCode.Plus never fired on German QWERTZ, and Ctrl+digits let the
    /// game silently pick a dialogue option, because it reads digits in conversations
    /// regardless of held Ctrl - full history in KeyBindings.) The keys drive the game's
    /// own HealingButton.ApplyHeal - the same code path as the click, so all game rules
    /// (charge consumption, animations, notifications) apply.
    /// </summary>
    public static class HealingKeyActions
    {
        public static void HealHealth() => Heal(SkillType.ENDURANCE, "HealWordHealth", "HealedHealth");
        public static void HealMorale() => Heal(SkillType.VOLITION, "HealWordMorale", "HealedMorale");

        private static void Heal(SkillType pool, string wordKey, string healedKey)
        {
            try
            {
                string word = Loc.Get(wordKey);

                var button = UnityEngine.Object.FindObjectsOfType<Il2Cpp.HealingButton>()
                    .FirstOrDefault(b => b != null && b.HealingPoolType == pool);
                if (button == null)
                {
                    // No button in the scene means the HUD (and with it healing) is not
                    // available right now - dialogs, cutscenes, menus.
                    TolkScreenReader.Instance.Speak(Loc.Get("HealNoButton"), true);
                    return;
                }

                int charges = GetCharges(pool);
                if (charges == 0)
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("HealNoCharges", word), true);
                    return;
                }

                // The game's own gate: false when the bar is already full (or healing is
                // temporarily not allowed). Same check the button makes for a click.
                if (!button.CanApplyHeal())
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("HealNotNeeded", word), true);
                    return;
                }

                button.ApplyHeal();

                // Remaining charges: the game consumes exactly one per ApplyHeal, so
                // derive the count from the value read BEFORE the heal. Re-reading here
                // is a trap (PR review): a delayed deduction still reports the old value
                // (+1 too many), and when the pre-heal read already failed (-1) there is
                // no number to derive - then say "restored" without inventing a count.
                if (charges > 0)
                {
                    int left = charges - 1;
                    TolkScreenReader.Instance.Speak(Loc.Get(healedKey, left), true);
                    MelonLogger.Msg($"[HEAL] {pool} healed via keyboard, {left} charges left");
                }
                else
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("HealedNoCount", word), true);
                    MelonLogger.Msg($"[HEAL] {pool} healed via keyboard, charge count unknown");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error healing {pool}: {ex}");
            }
        }

        /// <summary>
        /// Healing charges left for a pool, -1 when unreadable (pools not up yet).
        /// THE one healing-charge lookup - the character sheet announcement and the dev
        /// bridge use it too; it existed three times before (PR review cleanup).
        /// </summary>
        public static int GetCharges(SkillType pool)
        {
            try
            {
                var player = PlayerCharacter.Singleton;
                if (player?.healingPools == null) return -1;
                return player.healingPools.GetHealingChargetsForSkill(pool);
            }
            catch
            {
                return -1;
            }
        }
    }
}
