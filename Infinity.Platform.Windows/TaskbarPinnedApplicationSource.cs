using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Platform.Windows;

public sealed class TaskbarPinnedApplicationSource(ILogger<TaskbarPinnedApplicationSource> logger) : ITaskbarPinnedApplicationSource
{
    private static readonly string TaskbarPinsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

    public Task<IReadOnlyList<LaunchableApplication>> GetPinnedApplicationsAsync(IReadOnlyList<LaunchableApplication> availableApplications, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(availableApplications);
        return Task.Run<IReadOnlyList<LaunchableApplication>>(() =>  {  cancellationToken.ThrowIfCancellationRequested();  try  {  if (!Directory.Exists(TaskbarPinsPath))  {  return[];  }   string[] shortcuts = Directory.EnumerateFiles(TaskbarPinsPath, "*.lnk", SearchOption.TopDirectoryOnly).OrderBy(path => File.GetLastWriteTimeUtc(path)).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();  cancellationToken.ThrowIfCancellationRequested();  return TaskbarPinnedApplicationMatcher.Match(shortcuts, availableApplications);  }  catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)  {  throw;  }  catch (Exception exception)when (exception is IOException or UnauthorizedAccessException)  {  logger.LogDebug(exception, "The taskbar pinned shortcut folder could not be read");  return[];  }  }, cancellationToken);
    }
}
