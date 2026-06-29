using Infinity.Application.Abstractions;

namespace Infinity.Shell;

public class WindowFilterEffectController(IEnumerable<IWindowFilterEffects> effects) :
    IWindowFilterEffectController
{
    public void Apply()
    {
        foreach (IWindowFilterEffects effect in effects)
        {
            effect.Apply();
        }
    }

    public void Clear()
    {
        foreach (IWindowFilterEffects effect in effects)
        {
            effect.Clear();
        }
    }
}