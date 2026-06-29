using Infinity.Platform.Abstractions;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.Platform.Windows;

public class WindowOpacity :
    IWindowOpacity
{
    private readonly Dictionary<nint, WindowOpacityAnimation> activeAnimations = [];
    private readonly Dictionary<nint, byte> knownOpacities = [];
    private readonly object activeAnimationsSyncRoot = new();

    private const int WsExLayered = 0x00080000;
    private const int AnimationDurationMs = 300;
    private const int AnimationFrameMs = 8;

    public void SetOpacity(nint windowHandle, byte opacity)
    {
        if (windowHandle == default)
        {
            return;
        }

        StartAnimation(windowHandle, opacity, clearWhenCompleted: false);
    }

    public void ClearOpacity(nint windowHandle)
    {
        if (windowHandle == default)
        {
            return;
        }

        int extendedStyle = PInvoke.GetWindowLong(new HWND(windowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((extendedStyle & WsExLayered) == 0 && !knownOpacities.ContainsKey(windowHandle))
        {
            return;
        }

        StartAnimation(windowHandle, byte.MaxValue, clearWhenCompleted: true);
    }

    private void StartAnimation(nint windowHandle, byte targetOpacity, bool clearWhenCompleted)
    {
        WindowOpacityAnimation animation;

        lock (activeAnimationsSyncRoot)
        {
            if (activeAnimations.TryGetValue(windowHandle, out WindowOpacityAnimation? existingAnimation))
            {
                if (existingAnimation.TargetOpacity == targetOpacity && existingAnimation.ClearWhenCompleted == clearWhenCompleted)
                {
                    return;
                }

                existingAnimation.Cancellation.Cancel();

                animation = new WindowOpacityAnimation(windowHandle, existingAnimation.CurrentOpacity, targetOpacity, clearWhenCompleted);
                activeAnimations[windowHandle] = animation;
            }
            else
            {
                byte startOpacity = GetStartOpacity(windowHandle, targetOpacity);

                if (startOpacity == targetOpacity)
                {
                    if (clearWhenCompleted)
                    {
                        ClearLayered(windowHandle);
                        knownOpacities.Remove(windowHandle);
                    }

                    return;
                }

                animation = new WindowOpacityAnimation(windowHandle, startOpacity, targetOpacity, clearWhenCompleted);
                activeAnimations[windowHandle] = animation;
            }
        }

        _ = AnimateOpacityAsync(animation);
    }

    private byte GetStartOpacity(nint windowHandle, byte targetOpacity)
    {
        if (knownOpacities.TryGetValue(windowHandle, out byte knownOpacity))
        {
            return knownOpacity;
        }

        int extendedStyle = PInvoke.GetWindowLong(new HWND(windowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((extendedStyle & WsExLayered) == 0)
        {
            return byte.MaxValue;
        }

        return targetOpacity == byte.MaxValue ? targetOpacity : byte.MaxValue;
    }

    private async Task AnimateOpacityAsync(WindowOpacityAnimation animation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            if (!EnsureLayered(animation.WindowHandle))
            {
                RemoveAnimation(animation);
                return;
            }

            while (stopwatch.ElapsedMilliseconds < AnimationDurationMs)
            {
                if (animation.Cancellation.IsCancellationRequested)
                {
                    return;
                }

                double progress = stopwatch.Elapsed.TotalMilliseconds / AnimationDurationMs;
                double easedProgress = Ease(progress);
                byte opacity = Interpolate(animation.StartOpacity, animation.TargetOpacity, easedProgress);
                animation.CurrentOpacity = opacity;

                if (!PInvoke.SetLayeredWindowAttributes(new HWND(animation.WindowHandle), new COLORREF(0), opacity, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
                {
                    RemoveAnimation(animation);
                    return;
                }

                lock (activeAnimationsSyncRoot)
                {
                    knownOpacities[animation.WindowHandle] = opacity;
                }

                await Task.Delay(AnimationFrameMs);
            }

            if (animation.Cancellation.IsCancellationRequested)
            {
                return;
            }

            animation.CurrentOpacity = animation.TargetOpacity;

            if (!PInvoke.SetLayeredWindowAttributes(new HWND(animation.WindowHandle), new COLORREF(0), animation.TargetOpacity, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
            {
                RemoveAnimation(animation);
                return;
            }

            lock (activeAnimationsSyncRoot)
            {
                knownOpacities[animation.WindowHandle] = animation.TargetOpacity;
            }

            if (animation.ClearWhenCompleted)
            {
                ClearLayered(animation.WindowHandle);

                lock (activeAnimationsSyncRoot)
                {
                    knownOpacities.Remove(animation.WindowHandle);
                }
            }

            RemoveAnimation(animation);
        }
        finally
        {
            stopwatch.Stop();
            animation.Cancellation.Dispose();
        }
    }

    private bool EnsureLayered(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);
        int extendedStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((extendedStyle & WsExLayered) != 0)
        {
            return true;
        }

        int result = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, extendedStyle | WsExLayered);

        return result != 0;
    }

    private void ClearLayered(nint windowHandle)
    {
        HWND hwnd = new(windowHandle);
        int extendedStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

        if ((extendedStyle & WsExLayered) == 0)
        {
            return;
        }

        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, extendedStyle & ~WsExLayered);
    }

    private void RemoveAnimation(WindowOpacityAnimation animation)
    {
        lock (activeAnimationsSyncRoot)
        {
            if (activeAnimations.TryGetValue(animation.WindowHandle, out WindowOpacityAnimation? currentAnimation) &&
                ReferenceEquals(currentAnimation, animation))
            {
                activeAnimations.Remove(animation.WindowHandle);
            }
        }
    }

    private static byte Interpolate(byte startOpacity, byte targetOpacity, double progress)
    {
        double opacity = startOpacity + ((targetOpacity - startOpacity) * progress);

        if (opacity <= byte.MinValue)
        {
            return byte.MinValue;
        }

        if (opacity >= byte.MaxValue)
        {
            return byte.MaxValue;
        }

        return (byte)opacity;
    }

    private static double Ease(double progress)
    {
        if (progress <= 0)
        {
            return 0;
        }

        if (progress >= 1)
        {
            return 1;
        }

        return CubicBezier(progress, 0.4, 0.0, 0.2, 1.0);
    }

    private static double CubicBezier(double progress, double firstControlPointX, double firstControlPointY, double secondControlPointX, double secondControlPointY)
    {
        double lowerBound = 0;
        double upperBound = 1;
        double bezierProgress = progress;

        for (int index = 0; index < 8; index++)
        {
            bezierProgress = (lowerBound + upperBound) / 2;
            double currentProgress = Cubic(bezierProgress, 0, firstControlPointX, secondControlPointX, 1);

            if (currentProgress < progress)
            {
                lowerBound = bezierProgress;
            }
            else
            {
                upperBound = bezierProgress;
            }
        }

        return Cubic(bezierProgress, 0, firstControlPointY, secondControlPointY, 1);
    }

    private static double Cubic(double progress, double startValue, double firstControlPoint, double secondControlPoint, double endValue)
    {
        double inverseProgress = 1 - progress;

        return
            (inverseProgress * inverseProgress * inverseProgress * startValue) +
            (3 * inverseProgress * inverseProgress * progress * firstControlPoint) +
            (3 * inverseProgress * progress * progress * secondControlPoint) +
            (progress * progress * progress * endValue);
    }

    private sealed class WindowOpacityAnimation(nint windowHandle, byte startOpacity, byte targetOpacity, bool clearWhenCompleted)
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public nint WindowHandle { get; } = windowHandle;
        public byte StartOpacity { get; } = startOpacity;
        public byte TargetOpacity { get; } = targetOpacity;
        public bool ClearWhenCompleted { get; } = clearWhenCompleted;
        public byte CurrentOpacity { get; set; } = startOpacity;
    }
}