namespace Infinity.Shell;

public sealed record DesktopOverviewSessionState(bool IsOpen, bool StaysOpen, bool IsPreviewActive, bool IsCompletionRequested, bool IsReadyToClose);
