using Infinity.Platform.Abstractions;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Infinity.Shell.WinUI;

public sealed class DesktopWallpaperSurfaceProvider :
    IDisposable
{
    private readonly Dictionary<LoadedImageSurface, Task<LoadedImageSourceLoadStatus>> loadTasks = [];
    private readonly List<LoadedImageSurface> surfaces = [];
    private DesktopBackground? background;
    private LoadedImageSurface? currentSurface;
    private bool disposed;

    public LoadedImageSurface GetOrCreate(DesktopBackground requestedBackground)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (string.IsNullOrWhiteSpace(requestedBackground.Wallpaper))
        {
            throw new ArgumentException("A wallpaper path is required", nameof(requestedBackground));
        }

        if (background == requestedBackground && currentSurface is not null)
        {
            return currentSurface;
        }

        LoadedImageSurface surface = LoadedImageSurface.StartLoadFromUri(
            new Uri(requestedBackground.Wallpaper, UriKind.Absolute));
        TaskCompletionSource<LoadedImageSourceLoadStatus> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleLoadCompleted(LoadedImageSurface sender, LoadedImageSourceLoadCompletedEventArgs args)
        {
            sender.LoadCompleted -= HandleLoadCompleted;
            completion.TrySetResult(args.Status);
        }

        surface.LoadCompleted += HandleLoadCompleted;
        surfaces.Add(surface);
        loadTasks[surface] = completion.Task;
        background = requestedBackground;
        currentSurface = surface;
        return surface;
    }

    public Task<LoadedImageSourceLoadStatus> WaitForLoadAsync(LoadedImageSurface surface)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return loadTasks.TryGetValue(surface, out Task<LoadedImageSourceLoadStatus>? loadTask)
            ? loadTask
            : Task.FromResult(LoadedImageSourceLoadStatus.Other);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        foreach (LoadedImageSurface surface in surfaces)
        {
            surface.Dispose();
        }

        surfaces.Clear();
        loadTasks.Clear();
        currentSurface = null;
        background = null;
        GC.SuppressFinalize(this);
    }
}
