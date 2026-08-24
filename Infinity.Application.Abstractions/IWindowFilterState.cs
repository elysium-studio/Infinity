namespace Infinity.Application.Abstractions;

public interface IWindowFilterState
{
    bool IsActive { get; }

    string Filter { get; set; }

    bool IsMatch(string title);
}