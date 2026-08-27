using Microsoft.UI.Xaml;

namespace Infinity.Shell.WinUI;

internal readonly record struct DesktopApplicationPickerRequest(FrameworkElement Anchor, DesktopApplicationTarget Target);
