using Infinity.Application.Abstractions;

namespace Infinity.Application;

public sealed class WindowArrowSwitchGesture() :
    PageGesture<WindowArrowSwitchEventArgs>([0x25, 0x27], [], virtualKeyCode => new WindowArrowSwitchEventArgs(virtualKeyCode));
