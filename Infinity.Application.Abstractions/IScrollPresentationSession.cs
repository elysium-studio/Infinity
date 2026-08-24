namespace Infinity.Application.Abstractions;

public interface IScrollPresentationSession
{
    bool IsActive { get; }

    void Begin();

    void End();
}
