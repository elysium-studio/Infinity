using Elysium.Platform.Abstractions;
using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class DesktopHistoryShortcutController(IKeyboardInputSource keyboardInputSource,
    IDesktopNavigationHistory history,
    DesktopHistoryConfiguration configuration)
{
    private readonly Lock syncRoot = new();
    private readonly HashSet<int> activeTriggerKeys = [];
    private bool isStarted;

    public void Start()
    {
        lock (syncRoot)
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
        }

        keyboardInputSource.KeyDown += HandleKeyDown;
        keyboardInputSource.KeyUp += HandleKeyUp;
        configuration.Changed += HandleConfigurationChanged;
    }

    public void Stop()
    {
        lock (syncRoot)
        {
            if (!isStarted)
            {
                return;
            }

            isStarted = false;
            activeTriggerKeys.Clear();
        }

        keyboardInputSource.KeyDown -= HandleKeyDown;
        keyboardInputSource.KeyUp -= HandleKeyUp;
        configuration.Changed -= HandleConfigurationChanged;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs args)
    {
        DesktopHistoryConfigurationSnapshot snapshot = configuration.Current;

        if (!snapshot.Enabled || args.Handled)
        {
            return;
        }

        bool goBack = Matches(snapshot.BackShortcut, args.VirtualKeyCode);
        bool goForward = Matches(snapshot.ForwardShortcut, args.VirtualKeyCode);

        if (goBack == goForward)
        {
            return;
        }

        args.Handled = true;

        lock (syncRoot)
        {
            if (!isStarted || !activeTriggerKeys.Add(args.VirtualKeyCode))
            {
                return;
            }
        }

        if (goBack)
        {
            history.GoBack();
        }
        else
        {
            history.GoForward();
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs args)
    {
        lock (syncRoot)
        {
            if (activeTriggerKeys.Remove(args.VirtualKeyCode))
            {
                args.Handled = true;
            }
        }
    }

    private void HandleConfigurationChanged(DesktopHistoryConfigurationSnapshot snapshot)
    {
        lock (syncRoot)
        {
            activeTriggerKeys.Clear();
        }
    }

    private bool Matches(IReadOnlyList<IReadOnlyList<int>> shortcut, int triggerKey)
    {
        if (shortcut.Count < 2)
        {
            return false;
        }

        IReadOnlyList<int> triggerGroup = shortcut[^1];

        if (!triggerGroup.Contains(triggerKey))
        {
            return false;
        }

        for (int index = 0; index < shortcut.Count - 1; index++)
        {
            if (!shortcut[index].Any(keyboardInputSource.IsKeyDown))
            {
                return false;
            }
        }

        return true;
    }
}
