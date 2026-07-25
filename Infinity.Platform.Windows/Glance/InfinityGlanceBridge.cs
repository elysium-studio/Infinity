using Infinity.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Infinity.Platform.Windows;

public sealed class InfinityGlanceBridge(ILogger<InfinityGlanceBridge> logger) :
    BackgroundService,
    IInfinityGlanceBridge
{
    private readonly Lock synchronization = new();
    private readonly SemaphoreSlim updateSignal = new(0, 1);
    private InfinityPageNavigationState? latestState;
    private bool? latestVisibility;
    private InfinityPageNavigationSurface visibleSurfaces;
    private bool pageUpdatePending;
    private bool visibilityUpdatePending;
    private bool isPageNavigationAvailable;

    public bool IsPageNavigationAvailable
    {
        get
        {
            lock (synchronization)
            {
                return isPageNavigationAvailable;
            }
        }
    }

    public event EventHandler<InfinityGlanceAvailabilityChangedEventArgs>? AvailabilityChanged;

    public event EventHandler<InfinityGlanceMessageReceivedEventArgs>? MessageReceived;

    public void PublishPageNavigation(InfinityPageNavigationState state)
    {
        if (!TrySetLatestState(state))
        {
            return;
        }

        SignalUpdate();
    }

    public void SetPageNavigationSurfaceVisible(InfinityPageNavigationSurface surface, bool isVisible)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(surface, InfinityPageNavigationSurface.None);

        if (TrySetPageNavigationSurfaceVisibility(surface, isVisible))
        {
            SignalUpdate();
        }
    }

    internal bool TrySetPageNavigationSurfaceVisibility(InfinityPageNavigationSurface surface, bool isVisible)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(surface, InfinityPageNavigationSurface.None);

        lock (synchronization)
        {
            bool wasVisible = visibleSurfaces != InfinityPageNavigationSurface.None;
            visibleSurfaces = isVisible ? visibleSurfaces | surface : visibleSurfaces & ~surface;
            bool isNavigationVisible = visibleSurfaces != InfinityPageNavigationSurface.None;

            if (wasVisible == isNavigationVisible)
            {
                return false;
            }

            latestVisibility = isNavigationVisible;
            visibilityUpdatePending = true;
            pageUpdatePending |= isNavigationVisible && latestState is not null;
            return true;
        }
    }

    internal bool TrySetLatestState(InfinityPageNavigationState state)
    {
        lock (synchronization)
        {
            if (latestState == state)
            {
                return false;
            }

            latestState = state;
            pageUpdatePending = true;
            return true;
        }
    }

    internal (InfinityPageNavigationState? State, bool? Visibility) TakePendingUpdates()
    {
        lock (synchronization)
        {
            InfinityPageNavigationState? state = pageUpdatePending ? latestState : null;
            bool? visibility = visibilityUpdatePending ? latestVisibility : null;
            pageUpdatePending = false;
            visibilityUpdatePending = false;
            return (state, visibility);
        }
    }

    public override void Dispose()
    {
        updateSignal.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeClientStream pipe = new(".", GlanceBridgeProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.ConnectAsync(stoppingToken);
                await RunConnectionAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "The Infinity connection to Glance failed");
            }
            finally
            {
                UpdateAvailability(false);
            }

            try
            {
                await Task.Delay(300, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunConnectionAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(pipe, Encoding.UTF8, false, leaveOpen: true);
        using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true);

        GlanceBridgeWireMessage hello = new()
        {
            Kind = "hello",
            ProtocolVersion = GlanceBridgeProtocol.Version,
            ApplicationId = GlanceBridgeProtocol.ApplicationId,
            ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        };

        await WriteAsync(writer, hello, cancellationToken);

        using CancellationTokenSource connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task readerTask = ReadMessagesAsync(reader, connectionCancellation.Token);
        Task writerTask = WriteUpdatesAsync(writer, connectionCancellation.Token);
        await Task.WhenAny(readerTask, writerTask);
        connectionCancellation.Cancel();

        try
        {
            await Task.WhenAll(readerTask, writerTask);
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task ReadMessagesAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? json = await reader.ReadLineAsync(cancellationToken);

            if (json is null)
            {
                return;
            }

            GlanceBridgeWireMessage? message = JsonSerializer.Deserialize(json, GlanceBridgeJsonContext.Default.GlanceBridgeWireMessage);

            if (message is { Kind: "capabilities", ProtocolVersion: GlanceBridgeProtocol.Version })
            {
                bool available = message.Capabilities?.Contains(GlanceBridgeProtocol.PagesCapability, StringComparer.OrdinalIgnoreCase) == true;
                UpdateAvailability(available);

                if (available)
                {
                    QueueLatestUpdates();
                }
            }

            if (message is { Kind: "event", ProtocolVersion: GlanceBridgeProtocol.Version } &&
                !string.IsNullOrWhiteSpace(message.Capability) &&
                !string.IsNullOrWhiteSpace(message.Topic) &&
                message.Payload.ValueKind != JsonValueKind.Undefined)
            {
                MessageReceived?.Invoke(this, new InfinityGlanceMessageReceivedEventArgs(message.Capability, message.Topic, message.Payload.GetRawText()));
            }
        }
    }

    private async Task WriteUpdatesAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await updateSignal.WaitAsync(cancellationToken);

            if (!IsPageNavigationAvailable)
            {
                continue;
            }

            (InfinityPageNavigationState? state, bool? visibility) = TakePendingUpdates();

            if (visibility.HasValue)
            {
                GlanceBridgeWireMessage visibilityMessage = new()
                {
                    Kind = "publish",
                    ProtocolVersion = GlanceBridgeProtocol.Version,
                    Capability = GlanceBridgeProtocol.PagesCapability,
                    Topic = GlanceBridgeProtocol.PageNavigationVisibilityTopic,
                    Payload = JsonSerializer.SerializeToElement(new InfinityPageNavigationVisibility(visibility.Value), GlanceBridgeJsonContext.Default.InfinityPageNavigationVisibility)
                };

                await WriteAsync(writer, visibilityMessage, cancellationToken);
            }

            if (state is not null)
            {
                GlanceBridgeWireMessage pageMessage = new()
                {
                    Kind = "publish",
                    ProtocolVersion = GlanceBridgeProtocol.Version,
                    Capability = GlanceBridgeProtocol.PagesCapability,
                    Topic = GlanceBridgeProtocol.PageNavigationTopic,
                    Payload = JsonSerializer.SerializeToElement(state, GlanceBridgeJsonContext.Default.InfinityPageNavigationState)
                };

                await WriteAsync(writer, pageMessage, cancellationToken);
            }
        }
    }

    private void QueueLatestUpdates()
    {
        lock (synchronization)
        {
            pageUpdatePending = latestState is not null;
            visibilityUpdatePending = latestVisibility.HasValue;
        }

        SignalUpdate();
    }

    private void SignalUpdate()
    {
        if (updateSignal.CurrentCount == 0)
        {
            updateSignal.Release();
        }
    }

    private static async Task WriteAsync(StreamWriter writer, GlanceBridgeWireMessage message, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message, GlanceBridgeJsonContext.Default.GlanceBridgeWireMessage);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private void UpdateAvailability(bool value)
    {
        lock (synchronization)
        {
            if (isPageNavigationAvailable == value)
            {
                return;
            }

            isPageNavigationAvailable = value;
        }

        AvailabilityChanged?.Invoke(this, new InfinityGlanceAvailabilityChangedEventArgs(value));
    }
}
