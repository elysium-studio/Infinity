namespace Infinity.Application.Abstractions;

public interface IPageGesture
{
    IReadOnlyCollection<int> TriggerKeys { get; }

    IReadOnlyCollection<int> RequiredKeys { get; }

    void Invoke(int virtualKeyCode);
}
