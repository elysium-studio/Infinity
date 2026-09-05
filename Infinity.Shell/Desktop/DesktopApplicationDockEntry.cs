using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public sealed record DesktopApplicationDockEntry(LaunchableApplication Application, DesktopApplicationDockSource Source);
