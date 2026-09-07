namespace Infinity.Platform.Abstractions;

public sealed record WindowTextWord(
    string Text,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record WindowTextRecognition(
    string Text,
    IReadOnlyList<WindowTextWord> Words,
    double Angle = 0);
