namespace Infinity.Application.Abstractions;

public interface IDeltaScrollMotion : 
    IScrollMotion
{
    void AddDelta(double pixels);
}
