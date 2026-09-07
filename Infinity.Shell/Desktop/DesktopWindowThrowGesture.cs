namespace Infinity.Shell;

public sealed class DesktopWindowThrowGesture
{
    private readonly Sample[] samples = new Sample[64];
    private int next;
    private int count;
    private Sample? latest;
    private TimeSpan lastMovement;

    public void Update(double x, double y, TimeSpan now)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            Reset();
            return;
        }

        if (latest is { } previous && now < previous.Time)
        {
            Reset();
        }

        if (latest is not { } last || x != last.X || y != last.Y)
        {
            lastMovement = now;
        }

        latest = new(x, y, now);
        if (count > 0 && (now - samples[(next + samples.Length - 1) % samples.Length].Time).TotalMilliseconds < 4)
        {
            return;
        }

        samples[next] = latest.Value;
        next = (next + 1) % samples.Length;
        count = Math.Min(count + 1, samples.Length);
    }

    public int Release(TimeSpan now)
    {
        int direction = GetDirection(now);
        Reset();
        return direction;
    }

    public void Reset()
    {
        next = 0;
        count = 0;
        latest = null;
        lastMovement = default;
    }

    public static bool TryGetTargetPage(int sourcePage, int direction, int? maximumPageCount, out int targetPage)
    {
        long target = (long)sourcePage + direction;
        targetPage = sourcePage;
        if (sourcePage < 0 || direction is not (-1 or 1) || target < 0 || target > int.MaxValue || maximumPageCount.HasValue && target >= maximumPageCount.Value)
        {
            return false;
        }

        targetPage = (int)target;
        return true;
    }

    private int GetDirection(TimeSpan now)
    {
        if (latest is not { } end || now < end.Time || (now - lastMovement).TotalMilliseconds > 50)
        {
            return 0;
        }

        Sample? start = null;
        double previousX = 0;
        double horizontalTravel = 0;
        for (int index = 0; index < count; index++)
        {
            Sample sample = samples[(next - count + samples.Length + index) % samples.Length];
            if ((now - sample.Time).TotalMilliseconds > 120)
            {
                continue;
            }

            if (start is null)
            {
                start = sample;
                previousX = sample.X;
            }

            horizontalTravel += Math.Abs(sample.X - previousX);
            previousX = sample.X;
        }

        if (start is not { } first)
        {
            return 0;
        }

        horizontalTravel += Math.Abs(end.X - previousX);
        double elapsed = (now - first.Time).TotalSeconds;
        double deltaX = end.X - first.X;
        double distance = Math.Abs(deltaX);
        if (elapsed < 0.02 || distance < 64 || distance / elapsed < 1000 || distance < Math.Abs(end.Y - first.Y) * 1.75 || distance < horizontalTravel * 0.8)
        {
            return 0;
        }

        return Math.Sign(deltaX);
    }

    private readonly record struct Sample(double X, double Y, TimeSpan Time);
}
