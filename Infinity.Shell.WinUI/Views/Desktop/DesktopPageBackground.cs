using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media;

namespace Infinity.Shell.WinUI;

public sealed record DesktopPageBackground(LoadedImageSurface? Wallpaper, Brush? Fill);
