using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Infinity.Platform.Abstractions;

namespace Infinity.Application;

public class ModifiedScrollInput(IPointerInputSource pointer, 
    IModifierKeyState modifierKeyState) : 
    IScrollInputSource
{
    public event Action<int>? ScrollDeltaReceived;
    public event Action<double>? ScrollVelocityIdle;
    public event Action? MiddleButtonClicked;

    public void Start()
    {
        pointer.ScrollDeltaReceived += HandleScrollDelta;
        pointer.ScrollVelocityIdle += HandleScrollVelocityIdle;
        pointer.MiddleButtonClicked += HandleMiddleButtonClicked;
    }

    public void Stop()
    {
        pointer.ScrollDeltaReceived -= HandleScrollDelta;
        pointer.ScrollVelocityIdle -= HandleScrollVelocityIdle;
        pointer.MiddleButtonClicked -= HandleMiddleButtonClicked;
    }

    private void HandleScrollDelta(int delta) => ScrollDeltaReceived?.Invoke(delta);

    private void HandleScrollVelocityIdle(double velocity) => ScrollVelocityIdle?.Invoke(velocity);

    private void HandleMiddleButtonClicked()
    {
        if (modifierKeyState.IsActive)
        {
            MiddleButtonClicked?.Invoke();
        }
    }
}