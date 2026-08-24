namespace Infinity.Application.Abstractions;

public interface IWindowPageCoordinator :
    IWindowNavigationCoordinator,
    IForegroundWindowCoordinator
{
    void ExpectProgrammaticActivation(IntPtr handle);
}