namespace Infinity.Platform.Abstractions;

public interface IKeyLabelProvider
{
    string GetFullLabel(int keyCode);

    string GetShortLabel(int keyCode);

    string Shorten(string fullText);
}