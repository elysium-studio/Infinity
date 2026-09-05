using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Infinity.Platform.Windows;

public sealed class UserAssistApplicationUsageHistory(ILogger<UserAssistApplicationUsageHistory> logger) : IApplicationUsageHistory
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";
    private const string CountSubKeyName = "Count";

    public async Task<IReadOnlyList<LaunchableApplication>> GetMostUsedApplicationsAsync(IReadOnlyList<LaunchableApplication> applications, int maximumCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applications);
        if (maximumCount <= 0 || applications.Count == 0)
        {
            return[];
        }

        try
        {
            IReadOnlyList<UserAssistApplicationUsageEntry> entries = await Task.Run(ReadEntries, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return UserAssistApplicationMatcher.Match(applications, entries, maximumCount);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Windows application usage counts could not be read");
            return[];
        }
    }


    private static IReadOnlyList<UserAssistApplicationUsageEntry> ReadEntries()
    {
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(RegistryPath);
        if (root is null)
        {
            return[];
        }

        List<UserAssistApplicationUsageEntry> entries = [];
        foreach (string groupName in root.GetSubKeyNames())
        {
            using RegistryKey? countKey = root.OpenSubKey($@"{groupName}\{CountSubKeyName}");
            if (countKey is null)
            {
                continue;
            }

            foreach (string valueName in countKey.GetValueNames())
            {
                if (countKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)is not byte[] data)
                {
                    continue;
                }

                UserAssistApplicationUsageEntry? entry = UserAssistEntryParser.Parse(valueName, data);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }
}
