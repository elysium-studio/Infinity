using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace Infinity.Shell.WinUI;

public sealed class WindowCaptureAccess(ILogger<WindowCaptureAccess> logger)
{
    private Task<bool>? request;

    public Task<bool> RequestBorderlessAsync() => request ??= RequestAsync();

    private async Task<bool> RequestAsync()
    {
        if (!ApiInformation.IsMethodPresent("Windows.Graphics.Capture.GraphicsCaptureAccess", "RequestAccessAsync"))
        {
            return false;
        }

        try
        {
            AppCapabilityAccessStatus status = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
            if (status != AppCapabilityAccessStatus.Allowed)
            {
                logger.LogWarning("Borderless capture access returned {Status}; Windows will show its capture indicator", status);
            }

            return status == AppCapabilityAccessStatus.Allowed;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Borderless window capture is unavailable; retaining the system capture indicator");
            return false;
        }
    }
}
