namespace Infinity.Application.Abstractions;

public interface IPager
{
    event Action<int>? PageChanged;

    int CurrentPage { get; }

    int PageCount { get; }

    int? MaxPages { get; }

    void SetMaxPages(int? maxPages);

    void NavigateToPage(int page);

    void Start();

    void Stop();
}