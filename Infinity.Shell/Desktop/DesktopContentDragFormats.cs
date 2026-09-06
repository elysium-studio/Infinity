using Infinity.Platform.Abstractions;

namespace Infinity.Shell;

public static class DesktopContentDragFormats
{
    public static ContentDragKind Classify(IEnumerable<string> formats)
    {
        ContentDragKind kinds = ContentDragKind.None;
        foreach (string format in formats)
        {
            kinds |= format switch
            {
                "StorageItems" => ContentDragKind.Files,
                "FileGroupDescriptorW" or "FileGroupDescriptor" or "FileContents" => ContentDragKind.VirtualFiles,
                "Text" or "HTML Format" or "Rich Text Format" => ContentDragKind.Text,
                "WebLink" or "ApplicationLink" or "UniformResourceLocatorW" or "UniformResourceLocator" => ContentDragKind.Link,
                "Bitmap" or "PNG" => ContentDragKind.Image,
                _ => ContentDragKind.Other
            };
        }
        return kinds == ContentDragKind.None ? ContentDragKind.Other : kinds;
    }

}
