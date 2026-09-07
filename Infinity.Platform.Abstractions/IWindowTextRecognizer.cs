namespace Infinity.Platform.Abstractions;

public interface IWindowTextRecognizer
{
    Task<WindowTextRecognition> RecognizeAsync(WindowContentSnapshot snapshot, CancellationToken cancellationToken);
}
