namespace Infinity.Platform.Abstractions;

public interface IWindowApplicationIdentityProvider
{
    bool TryGetApplicationId(IntPtr windowHandle, out string applicationId);
}
