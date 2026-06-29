using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowArrowSwitchGesture() :
    PageGesture<WindowArrowSwitchEventArgs>([0x25, 0x27], [], virtualKeyCode => new WindowArrowSwitchEventArgs(virtualKeyCode));
