namespace Infinity.Application.Abstractions;

public interface IScrollDeltaAccumulator
{
    bool IsEmpty { get; }

    void Add(double pixels);

    double DrainAndReset();
}
