using Microsoft.Extensions.Logging;
using Windows.Graphics.Capture;

namespace Infinity.Platform.Windows;

/// <summary>
/// Checks capture support when the overview services are constructed at startup.
/// Reads are deliberately free of COM calls: they also occur inside mouse hooks.
/// </summary>
public sealed class WindowCaptureSupport
{
    public WindowCaptureSupport(ILogger<WindowCaptureSupport> logger)
        : this(GraphicsCaptureSession.IsSupported, logger)
    {
    }

    public WindowCaptureSupport(Func<bool> probe, ILogger<WindowCaptureSupport> logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            IsSupported = probe();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot determine window capture support during startup; capture is unavailable");
            IsSupported = false;
        }
    }

    public bool IsSupported { get; }
}
