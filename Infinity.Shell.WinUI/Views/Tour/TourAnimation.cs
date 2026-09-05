using System.Numerics;
using Microsoft.UI.Composition;

namespace Infinity.Shell.WinUI;

internal static class TourAnimation
{
    public static CubicBezierEasingFunction CreateEasing(Compositor compositor) => compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1), new Vector2(0.3f, 1));
}
