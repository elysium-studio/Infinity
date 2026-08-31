namespace Infinity.Application.Abstractions;

public interface ITextLocalizer
{
    string GetText(string key, params object[] arguments);
}
