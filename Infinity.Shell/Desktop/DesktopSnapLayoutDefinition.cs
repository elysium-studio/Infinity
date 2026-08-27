namespace Infinity.Shell;

public sealed record DesktopSnapLayoutDefinition(DesktopSnapLayoutKind Kind, IReadOnlyList<DesktopSnapSlot> Slots);
