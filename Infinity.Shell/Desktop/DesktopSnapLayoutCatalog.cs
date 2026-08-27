namespace Infinity.Shell;

public readonly record struct DesktopSnapSlot(double X, double Y, double Width, double Height);

public sealed record DesktopSnapLayoutDefinition(DesktopSnapLayoutKind Kind, IReadOnlyList<DesktopSnapSlot> Slots);

public sealed class DesktopSnapLayoutCatalog
{
    private static readonly IReadOnlyDictionary<DesktopSnapLayoutKind, DesktopSnapLayoutDefinition> Definitions =
        new Dictionary<DesktopSnapLayoutKind, DesktopSnapLayoutDefinition>
        {
            [DesktopSnapLayoutKind.Halves] = new(DesktopSnapLayoutKind.Halves,
            [
                new(0, 0, 0.5, 1),
                new(0.5, 0, 0.5, 1)
            ]),
            [DesktopSnapLayoutKind.Thirds] = new(DesktopSnapLayoutKind.Thirds,
            [
                new(0, 0, 1.0 / 3, 1),
                new(1.0 / 3, 0, 1.0 / 3, 1),
                new(2.0 / 3, 0, 1.0 / 3, 1)
            ]),
            [DesktopSnapLayoutKind.Quarters] = new(DesktopSnapLayoutKind.Quarters,
            [
                new(0, 0, 0.5, 0.5),
                new(0.5, 0, 0.5, 0.5),
                new(0, 0.5, 0.5, 0.5),
                new(0.5, 0.5, 0.5, 0.5)
            ]),
            [DesktopSnapLayoutKind.MainAndStack] = new(DesktopSnapLayoutKind.MainAndStack,
            [
                new(0, 0, 0.5, 1),
                new(0.5, 0, 0.5, 0.5),
                new(0.5, 0.5, 0.5, 0.5)
            ]),
            [DesktopSnapLayoutKind.PrimaryAndSide] = new(DesktopSnapLayoutKind.PrimaryAndSide,
            [
                new(0, 0, 2.0 / 3, 1),
                new(2.0 / 3, 0, 1.0 / 3, 1)
            ]),
            [DesktopSnapLayoutKind.PriorityColumns] = new(DesktopSnapLayoutKind.PriorityColumns,
            [
                new(0, 0, 0.25, 1),
                new(0.25, 0, 0.5, 1),
                new(0.75, 0, 0.25, 1)
            ]),
            [DesktopSnapLayoutKind.FourColumns] = new(DesktopSnapLayoutKind.FourColumns,
            [
                new(0, 0, 0.25, 1),
                new(0.25, 0, 0.25, 1),
                new(0.5, 0, 0.25, 1),
                new(0.75, 0, 0.25, 1)
            ]),
            [DesktopSnapLayoutKind.WidePriorityColumns] = new(DesktopSnapLayoutKind.WidePriorityColumns,
            [
                new(0, 0, 0.2, 1),
                new(0.2, 0, 0.3, 1),
                new(0.5, 0, 0.3, 1),
                new(0.8, 0, 0.2, 1)
            ]),
            [DesktopSnapLayoutKind.MainAndGrid] = new(DesktopSnapLayoutKind.MainAndGrid,
            [
                new(0, 0, 0.5, 1),
                new(0.5, 0, 0.25, 0.5),
                new(0.75, 0, 0.25, 0.5),
                new(0.5, 0.5, 0.25, 0.5),
                new(0.75, 0.5, 0.25, 0.5)
            ]),
            [DesktopSnapLayoutKind.Rows] = new(DesktopSnapLayoutKind.Rows,
            [
                new(0, 0, 1, 0.5),
                new(0, 0.5, 1, 0.5)
            ]),
            [DesktopSnapLayoutKind.PrimaryAndBottom] = new(DesktopSnapLayoutKind.PrimaryAndBottom,
            [
                new(0, 0, 1, 2.0 / 3),
                new(0, 2.0 / 3, 1, 1.0 / 3)
            ]),
            [DesktopSnapLayoutKind.ThirdRows] = new(DesktopSnapLayoutKind.ThirdRows,
            [
                new(0, 0, 1, 1.0 / 3),
                new(0, 1.0 / 3, 1, 1.0 / 3),
                new(0, 2.0 / 3, 1, 1.0 / 3)
            ]),
            [DesktopSnapLayoutKind.MainAndBottomStack] = new(DesktopSnapLayoutKind.MainAndBottomStack,
            [
                new(0, 0, 1, 0.5),
                new(0, 0.5, 0.5, 0.5),
                new(0.5, 0.5, 0.5, 0.5)
            ])
        };

    private static readonly DesktopSnapLayoutKind[] CompactLandscape =
    [
        DesktopSnapLayoutKind.Halves,
        DesktopSnapLayoutKind.PrimaryAndSide
    ];

    private static readonly DesktopSnapLayoutKind[] StandardLandscape =
    [
        DesktopSnapLayoutKind.Halves,
        DesktopSnapLayoutKind.PrimaryAndSide,
        DesktopSnapLayoutKind.MainAndStack,
        DesktopSnapLayoutKind.Quarters
    ];

    private static readonly DesktopSnapLayoutKind[] LargeLandscape =
    [
        DesktopSnapLayoutKind.Halves,
        DesktopSnapLayoutKind.PrimaryAndSide,
        DesktopSnapLayoutKind.Thirds,
        DesktopSnapLayoutKind.MainAndStack,
        DesktopSnapLayoutKind.Quarters,
        DesktopSnapLayoutKind.PriorityColumns
    ];

    private static readonly DesktopSnapLayoutKind[] UltrawideLandscape =
    [
        .. LargeLandscape,
        DesktopSnapLayoutKind.FourColumns,
        DesktopSnapLayoutKind.WidePriorityColumns,
        DesktopSnapLayoutKind.MainAndGrid
    ];

    private static readonly DesktopSnapLayoutKind[] CompactPortrait =
    [
        DesktopSnapLayoutKind.Rows,
        DesktopSnapLayoutKind.PrimaryAndBottom
    ];

    private static readonly DesktopSnapLayoutKind[] LargePortrait =
    [
        DesktopSnapLayoutKind.Rows,
        DesktopSnapLayoutKind.PrimaryAndBottom,
        DesktopSnapLayoutKind.ThirdRows,
        DesktopSnapLayoutKind.MainAndBottomStack
    ];

    public DesktopSnapLayoutDefinition? Get(DesktopSnapLayoutKind kind) => Definitions.GetValueOrDefault(kind);

    public IReadOnlyList<DesktopSnapLayoutDefinition> GetAvailable(double width, double height, double rasterizationScale)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return [];
        }

        double scale = double.IsFinite(rasterizationScale) && rasterizationScale > 0 ? rasterizationScale : 1;
        double effectiveWidth = width / scale;
        double effectiveHeight = height / scale;
        double aspectRatio = width / height;
        IReadOnlyList<DesktopSnapLayoutKind> layouts;

        if (aspectRatio < 1)
        {
            layouts = effectiveHeight >= 1200 ? LargePortrait : CompactPortrait;
        }
        else if (aspectRatio >= 2 && effectiveWidth >= 2560)
        {
            layouts = UltrawideLandscape;
        }
        else if (effectiveWidth >= 1920)
        {
            layouts = LargeLandscape;
        }
        else if (effectiveWidth >= 1200)
        {
            layouts = StandardLandscape;
        }
        else
        {
            layouts = CompactLandscape;
        }

        return layouts.Select(kind => Definitions[kind]).ToArray();
    }

    public int HitTest(DesktopSnapLayoutKind kind, double normalizedX, double normalizedY)
    {
        DesktopSnapLayoutDefinition? definition = Get(kind);

        if (definition is null || !double.IsFinite(normalizedX) || !double.IsFinite(normalizedY))
        {
            return -1;
        }

        for (int index = 0; index < definition.Slots.Count; index++)
        {
            DesktopSnapSlot slot = definition.Slots[index];

            if (normalizedX >= slot.X && normalizedX <= slot.X + slot.Width && normalizedY >= slot.Y && normalizedY <= slot.Y + slot.Height)
            {
                return index;
            }
        }

        return -1;
    }
}
