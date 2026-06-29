using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infinity.Application;

public class PageGestureSource(IKeyboardInputSource keyboardInputSource,
    IModifierKeyState modifierKeyState,
    IEnumerable<IPageGesture> gestures,
    ILogger<PageGestureSource> logger) :
    IPageGestureSource
{
    private readonly List<IPageGesture> registeredGestures = gestures.ToList();
    private readonly HashSet<int> triggerKeys = gestures.SelectMany(gesture => gesture.TriggerKeys).ToHashSet();
    private readonly HashSet<int> activeTriggerKeys = [];
    private bool sessionActive;

    public event Action? SessionStarted;

    public event Action? SessionEnded;

    public void Start()
    {
        keyboardInputSource.KeyDown += HandleKeyDown;
        keyboardInputSource.KeyUp += HandleKeyUp;
        modifierKeyState.StateChanged += HandleModifierStateChanged;
    }

    public void Stop()
    {
        keyboardInputSource.KeyDown -= HandleKeyDown;
        keyboardInputSource.KeyUp -= HandleKeyUp;
        modifierKeyState.StateChanged -= HandleModifierStateChanged;
        activeTriggerKeys.Clear();
        sessionActive = false;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        if (!modifierKeyState.IsActive)
        {
            return;
        }

        if (!triggerKeys.Contains(args.VirtualKeyCode))
        {
            return;
        }

        args.Handled = true;

        if (!activeTriggerKeys.Add(args.VirtualKeyCode))
        {
            return;
        }

        IPageGesture? gesture = registeredGestures
            .Where(candidate => candidate.TriggerKeys.Contains(args.VirtualKeyCode))
            .Where(candidate => candidate.RequiredKeys.Count == 0 || candidate.RequiredKeys.Any(keyboardInputSource.IsKeyDown))
            .OrderByDescending(candidate => candidate.RequiredKeys.Count)
            .FirstOrDefault();

        if (gesture is null)
        {
            return;
        }

        if (!sessionActive)
        {
            sessionActive = true;
            logger.LogDebug("Page gesture session started");
            SessionStarted?.Invoke();
        }

        logger.LogDebug("Firing page gesture for key {Key}", args.VirtualKeyCode);

        gesture.Invoke(args.VirtualKeyCode);
    }

    private void HandleKeyUp(object? sender, KeyEventArgs args)
    {
        if (!triggerKeys.Contains(args.VirtualKeyCode))
        {
            return;
        }

        if (activeTriggerKeys.Remove(args.VirtualKeyCode))
        {
            args.Handled = true;
        }
    }

    private void HandleModifierStateChanged(bool isActive)
    {
        if (isActive || !sessionActive)
        {
            return;
        }

        sessionActive = false;
        activeTriggerKeys.Clear();
        logger.LogDebug("Page gesture session ended");
        SessionEnded?.Invoke();
    }
}