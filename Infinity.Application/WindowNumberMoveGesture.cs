using Infinity.Application.Abstractions;

namespace Infinity.Application;

public class WindowNumberMoveGesture() :
    PageGesture<WindowNumberMoveEventArgs>([0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39], [0x10, 0xA0, 0xA1], virtualKeyCode => new WindowNumberMoveEventArgs(virtualKeyCode));
