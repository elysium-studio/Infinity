namespace Infinity.Platform.Abstractions;

public interface IWindowIconSource
{
    Task<ApplicationIcon?> GetIconAsync(nint windowHandle);
}
