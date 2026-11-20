namespace Batto.Models;

/// <summary>One entry of the icon-type picker shown for every device.</summary>
public sealed record DeviceTypeOption(DeviceType Type, string Glyph, string Label)
{
    public static readonly IReadOnlyList<DeviceTypeOption> All = new[]
    {
        new DeviceTypeOption(DeviceType.Headphones, "🎧", "Fone de ouvido"),
        new DeviceTypeOption(DeviceType.Headset, "🎧", "Headset"),
        new DeviceTypeOption(DeviceType.Speaker, "🔊", "Caixa de som"),
        new DeviceTypeOption(DeviceType.Mouse, "🖱", "Mouse"),
        new DeviceTypeOption(DeviceType.Keyboard, "⌨", "Teclado"),
        new DeviceTypeOption(DeviceType.Gamepad, "🎮", "Controle"),
        new DeviceTypeOption(DeviceType.Phone, "📱", "Celular"),
        new DeviceTypeOption(DeviceType.Pen, "🖊", "Caneta"),
        new DeviceTypeOption(DeviceType.Generic, "📶", "Genérico"),
    };
}
