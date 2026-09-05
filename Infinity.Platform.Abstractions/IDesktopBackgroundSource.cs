namespace Infinity.Platform.Abstractions;

public interface IDesktopBackgroundSource
{
    event EventHandler? BackgroundChanged;

    DesktopBackground GetBackground();

    void Start();

    void Stop();
}
