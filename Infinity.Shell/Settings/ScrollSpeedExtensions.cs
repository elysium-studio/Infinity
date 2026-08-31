namespace Infinity.Shell;

public static class ScrollSpeedExtensions
{
    extension(ScrollSpeed speed)
    {
        public double ToPixelsPerNotch() => speed switch
        {
            ScrollSpeed.Slow => 150.0,
            ScrollSpeed.Normal => 300.0,
            ScrollSpeed.Fast => 500.0,
            ScrollSpeed.VeryFast => 750.0,
            _ => 300.0
        };
    }
}