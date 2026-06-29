namespace Infinity.Application.Abstractions;

public interface ITrackedWindowFilter
{
    string Filter { get; set; }

    bool IsActive { get; }

    bool IsMatch(string title);
}