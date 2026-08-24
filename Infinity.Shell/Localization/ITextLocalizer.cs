namespace Infinity.Shell;

public interface ITextLocalizer
{
    string GetText(string key, params object[] arguments);
}