namespace Infinity.Application.Abstractions;

public interface IWindowPlacementRules
{
    bool CanCreateRule(IntPtr windowHandle);

    bool TryGetTargetPage(IntPtr windowHandle, out int targetPage);

    Task<bool> RemoveAsync(IntPtr windowHandle);

    Task<bool> SetTargetPageAsync(IntPtr windowHandle, int targetPage);
}