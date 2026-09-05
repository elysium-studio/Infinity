namespace Infinity.Platform.Windows;

internal sealed record UserAssistApplicationUsageEntry(string Identifier, int UseCount, DateTime LastUsedUtc);
