namespace Infinity.Platform.Abstractions;

public interface IWindowAncestorResolver
{
    IntPtr GetRootAncestor(IntPtr windowHandle);
}