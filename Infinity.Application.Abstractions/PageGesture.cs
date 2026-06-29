namespace Infinity.Application.Abstractions;

public class PageGesture<TEventArgs>(IReadOnlyCollection<int> triggerKeys,
    IReadOnlyCollection<int> requiredKeys,
    Func<int, TEventArgs> createEventArgs) :
    IPageGesture
{
    public IReadOnlyCollection<int> TriggerKeys => triggerKeys;

    public IReadOnlyCollection<int> RequiredKeys => requiredKeys;


    public event Action<TEventArgs>? Invoked;

    public void Invoke(int virtualKeyCode) => Invoked?.Invoke(createEventArgs(virtualKeyCode));
}