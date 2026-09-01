namespace Infinity.Platform.Abstractions;

public interface IScrollInputSuppression
{
    bool IsSuppressed { get; }

    IDisposable Suppress();
}
