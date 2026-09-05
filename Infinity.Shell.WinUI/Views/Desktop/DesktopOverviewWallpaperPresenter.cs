using System;
using System.Numerics;
using System.Threading.Tasks;
using Infinity.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Infinity.Shell.WinUI;

public sealed class DesktopOverviewWallpaperPresenter(DesktopWallpaperSurfaceProvider wallpaperSurfaceProvider, DesktopWallpaperBrushFactory wallpaperBrushFactory, ILogger<DesktopOverviewWallpaperPresenter> logger) : IDisposable
{
    private readonly DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private DesktopBackground? background;
    private DesktopBackground? pendingBackground;
    private Task<bool>? pendingPreparation;
    private CompositionSurfaceBrush? imageBrush;
    private CompositionEffectFactory? effectFactory;
    private CompositionEffectBrush? effectBrush;
    private CompositionColorBrush? colourBrush;
    private SpriteVisual? visual;
    private FrameworkElement? host;
    private int preparationGeneration;
    private bool disposed;

    public Task<bool> PrepareAsync(FrameworkElement compositionHost, DesktopBackground requestedBackground)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (background == requestedBackground && visual is not null)
        {
            return Task.FromResult(true);
        }

        if (pendingBackground == requestedBackground && pendingPreparation is not null)
        {
            return pendingPreparation;
        }

        int generation = ++preparationGeneration;
        pendingBackground = requestedBackground;
        pendingPreparation = PrepareCoreAsync(compositionHost, requestedBackground, generation);
        return pendingPreparation;
    }


    public bool Attach(FrameworkElement element)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (visual is null)
        {
            return false;
        }

        if (!ReferenceEquals(host, element))
        {
            Detach();
            ElementCompositionPreview.SetElementChildVisual(element, visual);
            host = element;
        }

        return true;
    }


    public void Detach()
    {
        if (host is null)
        {
            return;
        }

        ElementCompositionPreview.SetElementChildVisual(host, null);
        host = null;
    }


    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        preparationGeneration++;
        Detach();
        ReleaseResources();
        GC.SuppressFinalize(this);
    }


    private async Task<bool> PrepareCoreAsync(FrameworkElement compositionHost, DesktopBackground requestedBackground, int generation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requestedBackground.Wallpaper))
            {
                return await CommitColourAsync(compositionHost, requestedBackground, generation);
            }

            LoadedImageSurface surface = await DispatchAsync(() => wallpaperSurfaceProvider.GetOrCreate(requestedBackground));
            LoadedImageSourceLoadStatus status = await wallpaperSurfaceProvider.WaitForLoadAsync(surface);
            if (status != LoadedImageSourceLoadStatus.Success)
            {
                logger.LogWarning("Failed to decode desktop wallpaper {Wallpaper}: {Status}", requestedBackground.Wallpaper, status);
                return await CommitColourAsync(compositionHost, requestedBackground, generation);
            }

            return await DispatchAsync(() => CommitWallpaper(compositionHost, requestedBackground, generation, surface));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to prepare desktop wallpaper {Wallpaper}", requestedBackground.Wallpaper);
            try
            {
                return await CommitColourAsync(compositionHost, requestedBackground, generation);
            }
            catch (Exception fallbackException)
            {
                logger.LogError(fallbackException, "Failed to prepare the desktop background fallback");
                return false;
            }
        }
        finally
        {
            if (generation == preparationGeneration)
            {
                pendingBackground = null;
                pendingPreparation = null;
            }
        }
    }


    private Task<bool> CommitColourAsync(FrameworkElement compositionHost, DesktopBackground requestedBackground, int generation) => DispatchAsync(() => CommitColour(compositionHost, requestedBackground, generation));

    private bool CommitWallpaper(FrameworkElement compositionHost, DesktopBackground requestedBackground, int generation, LoadedImageSurface surface)
    {
        if (!CanCommit(generation))
        {
            return false;
        }

        Visual hostVisual = ElementCompositionPreview.GetElementVisual(compositionHost);
        Compositor compositor = hostVisual.Compositor;
        CompositionSurfaceBrush newImageBrush = wallpaperBrushFactory.Create(compositor, surface);
        GaussianBlurEffect blurEffect = new()
        {
            BlurAmount = 30,
            BorderMode = EffectBorderMode.Hard,
            Source = new CompositionEffectSourceParameter("Wallpaper")
        };
        CompositionEffectFactory newEffectFactory = compositor.CreateEffectFactory(blurEffect);
        CompositionEffectBrush newEffectBrush = newEffectFactory.CreateBrush();
        newEffectBrush.SetSourceParameter("Wallpaper", newImageBrush);
        SpriteVisual newVisual = compositor.CreateSpriteVisual();
        newVisual.Brush = newEffectBrush;
        newVisual.RelativeSizeAdjustment = Vector2.One;
        Detach();
        ReleaseResources();
        imageBrush = newImageBrush;
        effectFactory = newEffectFactory;
        effectBrush = newEffectBrush;
        visual = newVisual;
        background = requestedBackground;
        return true;
    }


    private bool CommitColour(FrameworkElement compositionHost, DesktopBackground requestedBackground, int generation)
    {
        if (!CanCommit(generation))
        {
            return false;
        }

        Visual hostVisual = ElementCompositionPreview.GetElementVisual(compositionHost);
        Compositor compositor = hostVisual.Compositor;
        CompositionColorBrush newColourBrush = compositor.CreateColorBrush(DesktopBackgroundColor.ParseOrDefault(requestedBackground.Colour));
        SpriteVisual newVisual = compositor.CreateSpriteVisual();
        newVisual.Brush = newColourBrush;
        newVisual.RelativeSizeAdjustment = Vector2.One;
        Detach();
        ReleaseResources();
        colourBrush = newColourBrush;
        visual = newVisual;
        background = requestedBackground;
        return true;
    }


    private bool CanCommit(int generation) => !disposed && generation == preparationGeneration;

    private Task<T> DispatchAsync<T>(Func<T> action)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(action());
        }

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("Desktop wallpaper dispatcher is unavailable"));
        }

        return completion.Task;
    }


    private void ReleaseResources()
    {
        if (visual is not null)
        {
            visual.Brush = null;
            visual.Dispose();
            visual = null;
        }

        effectBrush?.Dispose();
        effectBrush = null;
        effectFactory?.Dispose();
        effectFactory = null;
        imageBrush?.Dispose();
        imageBrush = null;
        colourBrush?.Dispose();
        colourBrush = null;
        background = null;
    }


}
