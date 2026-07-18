using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSunshine.Metric;
using Il2CppSunshine.Dialogue;
using MelonLoader;
using AccessibilityMod;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Utility class for announcing character status information
    /// </summary>
    public static class CharacterStatusAnnouncement
    {
        /// <summary>
        /// Announce comprehensive character status including health, morale, and healing items
        /// </summary>
        public static void AnnounceFullStatus()
        {
            try
            {
                // Get current health and morale using game's own calculation methods
                double currentHealth = CharacterLuaFunctions.CurrentEndurance();
                double currentMorale = CharacterLuaFunctions.CurrentVolition();

                // Get player character for max values and healing items
                var world = World.Singleton;
                if (world?.you == null)
                {
                    TolkScreenReader.Instance.Speak($"Health: {currentHealth:F0}, Morale: {currentMorale:F0}");
                    return;
                }

                var characterSheet = world.you;

                // Get max health and morale from skills using maximumValue
                var endurance = characterSheet.GetSkill(SkillType.ENDURANCE);
                var volition = characterSheet.GetSkill(SkillType.VOLITION);

                string announcement = $"Health: {currentHealth:F0}";
                if (endurance != null)
                {
                    announcement += $" of {endurance.maximumValue}";
                }

                announcement += $", Morale: {currentMorale:F0}";
                if (volition != null)
                {
                    announcement += $" of {volition.maximumValue}";
                }

                // Add healing items information - via the one shared charge lookup
                // (HealingKeyActions.GetCharges; it swallows its own errors and returns
                // -1 for "unreadable", which simply stays unannounced here).
                int healthCharges = HealingKeyActions.GetCharges(SkillType.ENDURANCE);
                int moraleCharges = HealingKeyActions.GetCharges(SkillType.VOLITION);

                if (healthCharges > 0 || moraleCharges > 0)
                {
                    announcement += ". Healing items: ";
                    if (healthCharges > 0)
                    {
                        announcement += $"{healthCharges} health";
                        if (moraleCharges > 0)
                        {
                            announcement += $", {moraleCharges} morale";
                        }
                    }
                    else if (moraleCharges > 0)
                    {
                        announcement += $"{moraleCharges} morale";
                    }
                }

                TolkScreenReader.Instance.Speak(announcement);
                MelonLogger.Msg($"Character status: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing character status: {ex}");
                TolkScreenReader.Instance.Speak("Could not get character status");
            }
        }
    }
}