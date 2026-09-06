namespace Infinity.Platform.Abstractions;

public interface IScrollInputSuppression
{
    bool IsSuppressed { get; }

    bool IsWheelSuppressed => IsSuppressed;

    IDisposable SuppressKeyboard() => Suppress();


    IDisposable Suppress();
}
