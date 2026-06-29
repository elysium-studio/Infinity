namespace Infinity.Application.Abstractions;

public interface ITrackedWindow :
    IDisposable
{
    double X { get; set; }

    double Y { get; set; }

    double Width { get; set; }

    double Height { get; set; }

    bool IsVisible { get; set; }

    bool IsFiltered { get; set; }

    bool IsSelected { get; set; }

    int? ZIndex { get; set; }

    string Title { get; set; }

    IntPtr Handle { get; }
}