using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Infinity.Shell;

public sealed partial class DesktopSnapLayoutOptionViewModel : ObservableObject
{
    private const double SlotSpacing = 4;

    [ObservableProperty]
    private bool isSelected;

    public DesktopSnapLayoutOptionViewModel(DesktopSnapLayoutDefinition definition, double previewWidth, double previewHeight)
    {
        Kind = definition.Kind;
        PreviewWidth = previewWidth;
        PreviewHeight = previewHeight;
        Slots = new ObservableCollection<DesktopSnapLayoutSlotViewModel>(definition.Slots.Select(slot => CreateSlot(slot, previewWidth, previewHeight)));
    }

    public DesktopSnapLayoutKind Kind { get; }

    public string Name => Kind.ToString();

    public double PreviewWidth { get; }

    public double PreviewHeight { get; }

    public ObservableCollection<DesktopSnapLayoutSlotViewModel> Slots { get; }

    public void SetHighlighted(bool value)
    {
        foreach (DesktopSnapLayoutSlotViewModel slot in Slots)
        {
            slot.IsHighlighted = value;
        }
    }

    private static DesktopSnapLayoutSlotViewModel CreateSlot(DesktopSnapSlot slot, double previewWidth, double previewHeight)
    {
        double halfSpacing = SlotSpacing / 2;
        double left = (slot.X * previewWidth) + (slot.X > 0 ? halfSpacing : 0);
        double top = (slot.Y * previewHeight) + (slot.Y > 0 ? halfSpacing : 0);
        double right = ((slot.X + slot.Width) * previewWidth) - (slot.X + slot.Width < 1 ? halfSpacing : 0);
        double bottom = ((slot.Y + slot.Height) * previewHeight) - (slot.Y + slot.Height < 1 ? halfSpacing : 0);

        return new DesktopSnapLayoutSlotViewModel(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
