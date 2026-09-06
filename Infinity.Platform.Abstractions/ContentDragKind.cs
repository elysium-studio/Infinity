namespace Infinity.Platform.Abstractions;

[Flags]
public enum ContentDragKind
{
    None = 0,
    Files = 1,
    VirtualFiles = 2,
    Text = 4,
    Link = 8,
    Image = 16,
    Other = 32
}
