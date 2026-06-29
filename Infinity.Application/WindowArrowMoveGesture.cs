using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowArrowMoveGesture() :
    PageGesture<WindowArrowMoveEventArgs>([0x25, 0x27], [0x10, 0xA0, 0xA1], virtualKeyCode => new WindowArrowMoveEventArgs(virtualKeyCode));
