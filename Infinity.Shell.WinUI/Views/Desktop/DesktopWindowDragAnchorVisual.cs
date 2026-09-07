using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;

namespace Infinity.Shell.WinUI;

internal sealed class DesktopWindowDragAnchorVisual : IDisposable
{
    private readonly UIElement[] elements;
    private readonly CompositionPropertySet pointerState;
    private readonly ExpressionAnimation translation;
    private Vector2? appliedPointer;
    private Vector2? appliedGrabOffset;

    public DesktopWindowDragAnchorVisual(Visual surface, Vector2 pointer, Vector2 grabOffset, float depth, params UIElement[] elements)
    {
        this.elements = elements;
        pointerState = surface.Compositor.CreatePropertySet();
        Update(pointer, grabOffset);
        translation = surface.Compositor.CreateExpressionAnimation("Vector3((pointerState.Pointer.X - surface.CenterPoint.X * (1 - surface.Scale.X)) / surface.Scale.X - pointerState.GrabOffset.X, (pointerState.Pointer.Y - surface.CenterPoint.Y * (1 - surface.Scale.Y)) / surface.Scale.Y - pointerState.GrabOffset.Y, depth)");
        translation.Target = nameof(UIElement.Translation);
        translation.SetReferenceParameter("surface", surface);
        translation.SetReferenceParameter("pointerState", pointerState);
        translation.SetScalarParameter("depth", depth);
        try
        {
            foreach (UIElement element in elements)
            {
                element.StartAnimation(translation);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Update(Vector2 pointer, Vector2 grabOffset)
    {
        if (appliedPointer != pointer)
        {
            pointerState.InsertVector2("Pointer", pointer);
            appliedPointer = pointer;
        }

        if (appliedGrabOffset != grabOffset)
        {
            pointerState.InsertVector2("GrabOffset", grabOffset);
            appliedGrabOffset = grabOffset;
        }
    }

    public void Dispose()
    {
        foreach (UIElement element in elements)
        {
            element.StopAnimation(translation);
        }

        translation.Dispose();
        pointerState.Dispose();
    }
}
