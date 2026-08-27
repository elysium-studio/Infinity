using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Infinity.Platform.Windows;

public sealed partial class ApplicationCatalog(ILogger<ApplicationCatalog> logger) :
    IApplicationCatalog,
    IApplicationLauncher
{
    private const string LibraryName = "Infinity.Platform.Windows.Native.dll";

    private readonly Lock syncRoot = new();
    private Task<IReadOnlyList<LaunchableApplication>>? cachedApplications;

    public Task<IReadOnlyList<LaunchableApplication>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<LaunchableApplication>> applications;

        lock (syncRoot)
        {
            applications = cachedApplications ??= Task.Run(EnumerateApplications);
        }

        return cancellationToken.CanBeCanceled
            ? applications.WaitAsync(cancellationToken)
            : applications;
    }

    public bool TryLaunch(LaunchableApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        try
        {
            int result = ApplicationLauncher_Launch(application.Id);

            if (result >= 0)
            {
                return true;
            }

            logger.LogWarning("The shell could not launch {ApplicationName}. HRESULT={Result}", application.DisplayName, result);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            logger.LogError(exception, "The application launcher is unavailable");
        }

        return false;
    }

    private IReadOnlyList<LaunchableApplication> EnumerateApplications()
    {
        nint buffer = 0;

        try
        {
            int result = ApplicationCatalog_Enumerate(out buffer, out int characterCount);

            if (result < 0 || buffer == 0 || characterCount <= 1)
            {
                logger.LogWarning("The shell application catalogue could not be read. HRESULT={Result}", result);
                return [];
            }

            string value = Marshal.PtrToStringUni(buffer, characterCount) ?? string.Empty;
            string[] fields = value.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            List<LaunchableApplication> applications = new(fields.Length / 2);

            for (int index = 0; index + 1 < fields.Length; index += 2)
            {
                applications.Add(new LaunchableApplication(fields[index], fields[index + 1]));
            }

            return applications;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            logger.LogError(exception, "The shell application catalogue is unavailable");
            return [];
        }
        finally
        {
            if (buffer != 0)
            {
                ApplicationCatalog_Free(buffer);
            }
        }
    }

    [LibraryImport(LibraryName, EntryPoint = "ApplicationCatalog_Enumerate")]
    private static partial int ApplicationCatalog_Enumerate(out nint buffer, out int characterCount);

    [LibraryImport(LibraryName, EntryPoint = "ApplicationCatalog_Free")]
    private static partial void ApplicationCatalog_Free(nint buffer);

    [LibraryImport(LibraryName, EntryPoint = "ApplicationLauncher_Launch", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int ApplicationLauncher_Launch(string parsingName);
}
