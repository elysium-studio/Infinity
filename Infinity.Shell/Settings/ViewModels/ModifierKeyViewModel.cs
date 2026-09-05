namespace Infinity.Shell;

public sealed record ModifierKeyViewModel(string Text, bool IsSymbol = false, string? ToolTip = null)
{
    public string FontFamily => IsSymbol ? "Segoe Fluent Icons" : "XamlAutoFontFamily";
}
