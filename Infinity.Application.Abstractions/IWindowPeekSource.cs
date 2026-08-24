namespace Infinity.Application.Abstractions;

public interface IWindowPeekSource :
    IPeekSource
{
    nint Handle { get; set; }
}