using RetroFootballManager.Core.Models;

namespace RetroFootballManager.Core.Common
{
    public static class BonusTypeInfo
    {
        public static readonly Dictionary<BonusType, string> DisplayNames = new()
        {
            { BonusType.PerWin, "Bonus pro Sieg" },
            { BonusType.PerPromotion, "Bonus für Aufstieg" },
            { BonusType.Midfield, "Bonus für Mittelfeldplatz" },
            { BonusType.Top5, "Bonus für Top‑5‑Platzierung" },
            { BonusType.MasterTitle, "Bonus für Meistertitel" },
            { BonusType.AvoidRelegation, "Bonus für Klassenerhalt" },
            {BonusType.None, "Kein extra Bonus" }
        };

        public static readonly Dictionary<BonusType, string> Descriptions = new()
        {
            { BonusType.PerWin, "Wird nach jedem gewonnenen Spiel ausgezahlt." },
            { BonusType.PerPromotion, "Wird ausgezahlt, wenn das Team eine Liga aufsteigt." },
            { BonusType.Midfield, "Wird ausgezahlt, wenn das Team im sicheren Mittelfeld landet." },
            { BonusType.Top5, "Wird ausgezahlt, wenn das Team unter den besten fünf landet." },
            { BonusType.MasterTitle, "Wird ausgezahlt, wenn das Team die Meisterschaft gewinnt." },
            { BonusType.AvoidRelegation, "Wird ausgezahlt, wenn das Team den Abstieg vermeidet." }
        };

        public static string GetDisplayName(BonusType type)
            => DisplayNames.TryGetValue(type, out var name) ? name : type.ToString();

        public static string GetDescription(BonusType type)
            => Descriptions.TryGetValue(type, out var desc) ? desc : "";
    }

}
