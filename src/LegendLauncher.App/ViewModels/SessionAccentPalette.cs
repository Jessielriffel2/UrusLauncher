using System.Windows.Media;

namespace LegendLauncher.App.ViewModels;

/// <summary>
/// Gives every running account its own accent color so the sidebar avatar and its
/// refresh/close controls can be told apart at a glance.
/// </summary>
internal static class SessionAccentPalette
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x35, 0xE5, 0xD8),
        Color.FromRgb(0x7C, 0x9C, 0xFF),
        Color.FromRgb(0xFF, 0xB4, 0x54),
        Color.FromRgb(0x6B, 0xE3, 0x8A),
        Color.FromRgb(0xFF, 0x6B, 0x9A),
        Color.FromRgb(0xC0, 0x8B, 0xFF),
        Color.FromRgb(0x4F, 0xD1, 0xE0),
        Color.FromRgb(0xFF, 0x8A, 0x5B),
    ];

    private static readonly Brush[] Brushes = Build(Palette);
    private static readonly Brush[] StrongBrushes = Build([.. Palette.Select(Brighten)]);
    private static readonly Brush[] SoftBrushes = Build([.. Palette.Select(Soften)]);

    public static int SlotCount => Palette.Length;

    public static Color GetColor(int slot) => Palette[Normalize(slot)];

    public static Brush GetBrush(int slot) => Brushes[Normalize(slot)];

    public static Brush GetStrongBrush(int slot) => StrongBrushes[Normalize(slot)];

    public static Brush GetSoftBrush(int slot) => SoftBrushes[Normalize(slot)];

    private static int Normalize(int slot)
    {
        int normalized = slot % Palette.Length;
        return normalized < 0 ? normalized + Palette.Length : normalized;
    }

    private static Color Brighten(Color color) => Color.FromRgb(
        (byte)Math.Clamp(color.R + ((255 - color.R) / 3), 0, 255),
        (byte)Math.Clamp(color.G + ((255 - color.G) / 3), 0, 255),
        (byte)Math.Clamp(color.B + ((255 - color.B) / 3), 0, 255));

    private static Color Soften(Color color) => Color.FromArgb(0x2E, color.R, color.G, color.B);

    private static Brush[] Build(IReadOnlyList<Color> colors)
    {
        Brush[] brushes = new Brush[colors.Count];
        for (int index = 0; index < colors.Count; index++)
        {
            var brush = new SolidColorBrush(colors[index]);
            brush.Freeze();
            brushes[index] = brush;
        }

        return brushes;
    }
}
