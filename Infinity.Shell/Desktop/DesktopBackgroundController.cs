using Elysium.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public class DesktopBackgroundController(IDesktopBackgroundSource source,
    IDispatcher dispatcher) :
    IDesktopBackgroundController
{
    private bool isSubscribed;

    public string? BackgroundColour { get; private set; }

    public string? BackgroundPath { get; private set; }

    public event EventHandler? BackgroundChanged;

    public void Subscribe()
    {
        if (isSubscribed)
        {
            return;
        }

        isSubscribed = true;
        source.BackgroundChanged += HandleSourceChanged;
        Apply(source.GetBackground());
    }

    public void Unsubscribe()
    {
        if (!isSubscribed)
        {
            return;
        }

        isSubscribed = false;
        source.BackgroundChanged -= HandleSourceChanged;
    }

    public void Clear()
    {
        dispatcher.Dispatch(() =>
        {
            BackgroundColour = string.Empty;
            BackgroundPath = string.Empty;
            BackgroundChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void HandleSourceChanged(object? sender, EventArgs args) =>
        Apply(source.GetBackground());

    private void Apply(DesktopBackground background)
    {
        dispatcher.Dispatch(() =>
        {
            BackgroundColour = background.Colour ?? string.Empty;
            BackgroundPath = background.Wallpaper ?? string.Empty;
            BackgroundChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}