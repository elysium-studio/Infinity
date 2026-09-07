using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace Infinity.Shell.WinUI;

public sealed partial class DesktopWindowSearchImage : UserControl
{
    private DesktopWindowSearchSnapshot? snapshot;
    private DesktopWindowSearchSnapshot? requestedSnapshot;
    private string query = string.Empty;
    private int generation;

    public DesktopWindowSearchImage() => InitializeComponent();

    public event Action<Exception>? Failed;
    public event Action? Ready;

    public void Clear()
    {
        if (snapshot is null && requestedSnapshot is null && SnapshotImage.Source is null)
        {
            return;
        }

        generation++;
        snapshot = null;
        requestedSnapshot = null;
        SnapshotImage.Source = null;
        Highlights.Children.Clear();
        Visibility = Visibility.Collapsed;
    }

    public void Show(DesktopWindowSearchSnapshot value, string search, CornerRadius cornerRadius)
    {
        Frame.CornerRadius = cornerRadius;
        if (ReferenceEquals(requestedSnapshot, value))
        {
            if (query != search)
            {
                query = search;
                UpdateHighlights();
            }
            return;
        }

        requestedSnapshot = value;
        query = search;
        int request = ++generation;
        _ = LoadAsync(value, request);
    }

    private async Task LoadAsync(DesktopWindowSearchSnapshot value, int request)
    {
        try
        {
            SoftwareBitmap bitmap = await Task.Run(() =>
            {
                SoftwareBitmap result = new(BitmapPixelFormat.Bgra8, value.Image.Width, value.Image.Height, BitmapAlphaMode.Premultiplied);
                try
                {
                    result.CopyFromBuffer(value.Image.Pixels.AsBuffer());
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }).ConfigureAwait(false);
            if (!DispatcherQueue.TryEnqueue(() => ApplyBitmap(bitmap, value, request)))
            {
                bitmap.Dispose();
            }
        }
        catch (Exception exception)
        {
            DispatcherQueue.TryEnqueue(() => ReportFailure(exception, request));
        }
    }

    private async void ApplyBitmap(SoftwareBitmap bitmap, DesktopWindowSearchSnapshot value, int request)
    {
        using (bitmap)
        {
            if (request != generation)
            {
                return;
            }

            try
            {
                SoftwareBitmapSource source = new();
                await source.SetBitmapAsync(bitmap);
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (request != generation)
                    {
                        return;
                    }

                    SnapshotImage.Source = source;
                    snapshot = value;
                    ImageLayout.Width = value.Image.Width;
                    ImageLayout.Height = value.Image.Height;
                    UpdateHighlights();
                    Visibility = Visibility.Visible;
                    Ready?.Invoke();
                });
            }
            catch (Exception exception)
            {
                DispatcherQueue.TryEnqueue(() => ReportFailure(exception, request));
            }
        }
    }

    private void ReportFailure(Exception exception, int request)
    {
        if (request == generation)
        {
            Clear();
            Failed?.Invoke(exception);
        }
    }

    private void UpdateHighlights()
    {
        Highlights.Children.Clear();
        if (snapshot is null)
        {
            return;
        }

        Highlights.RenderTransform = new RotateTransform { Angle = snapshot.Recognition.Angle, CenterX = snapshot.Image.Width / 2d, CenterY = snapshot.Image.Height / 2d };
        Style style = (Style)Resources["SearchWordHighlightStyle"];
        foreach (WindowTextWord word in snapshot.GetMatches(query))
        {
            if (!double.IsFinite(word.X) || !double.IsFinite(word.Y) || !double.IsFinite(word.Width) || !double.IsFinite(word.Height) || word.Width <= 0 || word.Height <= 0)
            {
                continue;
            }

            Border highlight = new() { Width = word.Width, Height = word.Height, Style = style };
            Canvas.SetLeft(highlight, word.X);
            Canvas.SetTop(highlight, word.Y);
            Highlights.Children.Add(highlight);
        }
    }
}
