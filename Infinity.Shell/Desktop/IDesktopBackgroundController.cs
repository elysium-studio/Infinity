namespace Infinity.Shell;

public interface IDesktopBackgroundController
{
    string? BackgroundColour { get; }

    string? BackgroundPath { get; }

    event EventHandler BackgroundChanged;

    void Subscribe();

    void Unsubscribe();

    void Clear();
}