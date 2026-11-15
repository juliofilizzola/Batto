using System.Drawing;

namespace Batto.Models;

public enum BatteryFaixa
{
    Unknown,
    Critical, // 0% - 19%
    Low,      // 20% - 39%
    Medium,   // 40% - 79%
    High      // 80% - 100%
}

public static class BatteryRangeHelper
{
    public static BatteryFaixa GetFaixa(int? batteryLevel)
    {
        if (!batteryLevel.HasValue || batteryLevel.Value < 0)
            return BatteryFaixa.Unknown;

        return batteryLevel.Value switch
        {
            >= 80 => BatteryFaixa.High,
            >= 40 => BatteryFaixa.Medium,
            >= 20 => BatteryFaixa.Low,
            _ => BatteryFaixa.Critical
        };
    }

    public static Color GetDrawingColor(BatteryFaixa faixa)
    {
        return faixa switch
        {
            BatteryFaixa.High => Color.FromArgb(34, 197, 94),     // #22C55E Emerald / Green
            BatteryFaixa.Medium => Color.FromArgb(56, 189, 248),  // #38BDF8 Sky Blue / Cyan
            BatteryFaixa.Low => Color.FromArgb(245, 158, 11),     // #F59E0B Amber / Orange
            BatteryFaixa.Critical => Color.FromArgb(239, 68, 68), // #EF4444 Red
            _ => Color.FromArgb(156, 163, 175)                    // #9CA3AF Gray
        };
    }

    public static string GetHexColor(BatteryFaixa faixa)
    {
        return faixa switch
        {
            BatteryFaixa.High => "#22C55E",
            BatteryFaixa.Medium => "#38BDF8",
            BatteryFaixa.Low => "#F59E0B",
            BatteryFaixa.Critical => "#EF4444",
            _ => "#9CA3AF"
        };
    }

    public static string GetLabel(BatteryFaixa faixa)
    {
        return faixa switch
        {
            BatteryFaixa.High => "Bateria Alta (80% - 100%)",
            BatteryFaixa.Medium => "Bateria Intermediária (40% - 79%)",
            BatteryFaixa.Low => "Bateria Baixa (20% - 39%)",
            BatteryFaixa.Critical => "Bateria Crítica (0% - 19%)",
            _ => "Desconhecido"
        };
    }
}
