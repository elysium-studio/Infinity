namespace Infinity.Shell;

public interface IDesktopApplicationDockOrderStore
{
    IReadOnlyList<string> ApplicationIdentifiers { get; }

    Task SaveAsync(
        IEnumerable<string> applicationIdentifiers,
        CancellationToken cancellationToken = default);
}
