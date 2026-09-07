using System.Runtime.InteropServices.WindowsRuntime;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Infinity.Platform.Windows;

public sealed class WindowTextRecognizer(
    ILogger<WindowTextRecognizer> logger) : IWindowTextRecognizer
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private OcrEngine? engine;
    private bool initialized;

    public async Task<WindowTextRecognition> RecognizeAsync(WindowContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!initialized)
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
                initialized = true;
                if (engine is null)
                {
                    logger.LogWarning("Window content search is unavailable because no matching Windows OCR language is installed");
                }
            }

            if (engine is null || snapshot.Width <= 0 || snapshot.Height <= 0 || snapshot.Width > OcrEngine.MaxImageDimension || snapshot.Height > OcrEngine.MaxImageDimension || (long)snapshot.Width * snapshot.Height * 4 != snapshot.Pixels.Length)
            {
                return new(string.Empty, []);
            }

            using SoftwareBitmap bitmap = new(BitmapPixelFormat.Bgra8, snapshot.Width, snapshot.Height, BitmapAlphaMode.Ignore);
            bitmap.CopyFromBuffer(snapshot.Pixels.AsBuffer());
            OcrResult result = await engine.RecognizeAsync(bitmap);
            cancellationToken.ThrowIfCancellationRequested();
            WindowTextWord[] words = [..result.Lines.SelectMany(line => line.Words).Take(12000).Select(word => new WindowTextWord(word.Text, word.BoundingRect.X, word.BoundingRect.Y, word.BoundingRect.Width, word.BoundingRect.Height))];
            return new(result.Text.Length <= 65536 ? result.Text : result.Text[..65536], words, result.TextAngle ?? 0);
        }
        finally
        {
            gate.Release();
        }
    }
}
