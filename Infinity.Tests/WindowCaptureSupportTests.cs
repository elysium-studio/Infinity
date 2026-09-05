using System.Runtime.InteropServices;
using Infinity.Platform.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infinity.Tests;

public sealed class WindowCaptureSupportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SupportIsProbedAtConstructionAndNeverFromTheGetter(bool available)
    {
        int calls = 0;
        bool insideInputHook = false;
        WindowCaptureSupport support = new(() =>  {  Assert.False(insideInputHook);  calls++;  return available;  }, NullLogger<WindowCaptureSupport>.Instance);
        Assert.Equal(1, calls);
        insideInputHook = true;
        for (int index = 0; index < 10; index++)
        {
            Assert.Equal(available, support.IsSupported);
        }

        Assert.Equal(1, calls);
    }


    [Fact]
    public void FailedProbeIsContainedAndNotRetriedByInput()
    {
        int calls = 0;
        WindowCaptureSupport support = new(() =>  {  calls++;  throw new COMException("Cannot make an outgoing call", unchecked((int)0x8001010D));  }, NullLogger<WindowCaptureSupport>.Instance);
        Assert.False(support.IsSupported);
        Assert.False(support.IsSupported);
        Assert.Equal(1, calls);
    }
}
