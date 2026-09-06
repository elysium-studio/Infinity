#include "../../Infinity.Platform.Windows.Native/ContentDragProbe.cpp"
#include <cassert>

int main()
{
    ContentDragProbe* probe = new ContentDragProbe();
    void* queried = nullptr;
    assert(probe->QueryInterface(IID_IDropTarget, &queried) == S_OK);
    assert(queried == static_cast<IDropTarget*>(probe));
    assert(static_cast<IDropTarget*>(queried)->Release() == 1);
    assert(probe->QueryInterface(IID_IDataObject, &queried) == E_NOINTERFACE);
    assert(queried == nullptr);
    assert(probe->QueryInterface(IID_IUnknown, nullptr) == E_POINTER);

    DWORD effect = DROPEFFECT_COPY | DROPEFFECT_MOVE | DROPEFFECT_LINK;
    assert(probe->DragEnter(nullptr, MK_LBUTTON, {}, &effect) == S_OK);
    assert(effect == DROPEFFECT_NONE);
    assert(probe->kinds != 0);
    assert(ContentDragProbe_Poll(probe) == probe->kinds);

    effect = DROPEFFECT_MOVE;
    assert(probe->DragOver(MK_LBUTTON | MK_SHIFT, {}, &effect) == S_OK);
    assert(effect == DROPEFFECT_NONE);
    assert(probe->DragLeave() == S_OK);
    assert(probe->kinds == 0);

    assert(probe->DragEnter(nullptr, MK_RBUTTON, {}, &effect) == S_OK);
    effect = DROPEFFECT_COPY;
    assert(probe->Drop(nullptr, 0, {}, &effect) == S_OK);
    assert(effect == DROPEFFECT_NONE);
    assert(probe->kinds == 0);
    assert(probe->Release() == 0);

    assert(ContentDragProbe_Create(nullptr) == E_POINTER);
    assert(ContentDragProbe_Poll(nullptr) == 0);
    ContentDragProbe_Destroy(nullptr);
    assert(ProbeWindowProc(nullptr, WM_MOUSEACTIVATE, 0, 0) == MA_NOACTIVATE);
    return 0;
}
