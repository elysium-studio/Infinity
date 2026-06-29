namespace Elysium.Presentation.Abstractions;

public interface ITourViewModel : 
    IDisposable
{
    bool CanGoBack { get; }

    bool CanGoNext { get; }
}