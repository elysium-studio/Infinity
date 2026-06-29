namespace Infinity.Application.Abstractions;

public interface IVelocityScrollMotion : 
    IScrollMotion
{
    void AddVelocity(double pixelsPerTick);
}