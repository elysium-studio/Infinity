using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Infinity.UI.WinUI;

internal sealed class DesktopOverlayResponsivenessMonitor : IDisposable
{
    private const long TimeoutMilliseconds = 5000;
    private readonly HWND[] handles;
    private readonly DispatcherQueue dispatcher;
    private readonly Action dismiss;
    private readonly object gate = new();
    private long lastResponse;
    private int generation;
    private bool enabled;
    private bool pingPending;
    private volatile bool tripped;
    private bool disposed;

    public bool IsEmergencyHidden => tripped;

    public DesktopOverlayResponsivenessMonitor(HWND[] handles, DispatcherQueue dispatcher, Action dismiss)
    {
        this.handles = handles;
        this.dispatcher = dispatcher;
        this.dismiss = dismiss;
        new Thread(Run)
        {
            IsBackground = true,
            Name = "Infinity overlay responsiveness"
        }.Start();
    }


    public void Start()
    {
        lock (gate)
        {
            if (disposed || enabled)
            {
                return;
            }

            generation++;
            lastResponse = Environment.TickCount64;
            enabled = true;
            pingPending = false;
            tripped = false;
            Monitor.PulseAll(gate);
        }
    }


    public void Stop()
    {
        lock (gate)
        {
            enabled = false;
            generation++;
        }
    }


    private unsafe void Run()
    {
        lock (gate)
        {
            while (!disposed)
            {
                Monitor.Wait(gate, 250);
                if (disposed)
                {
                    return;
                }

                if (!enabled)
                {
                    continue;
                }

                int current = generation;
                if (!tripped && Environment.TickCount64 - lastResponse >= TimeoutMilliseconds)
                {
                    tripped = true;
                    dispatcher.TryEnqueue(() =>
                    {
                        lock (gate)
                        {
                            if (!enabled || generation != current)
                            {
                                return;
                            }
                        }

                        dismiss();
                    });
                }

                if (tripped)
                {
                    foreach (HWND handle in handles)
                    {
                        PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
                    }

                    PInvoke.ClipCursor(null);
                    continue;
                }

                if (pingPending)
                {
                    continue;
                }

                pingPending = true;
                dispatcher.TryEnqueue(() =>
                {
                    lock (gate)
                    {
                        if (!enabled || generation != current || tripped)
                        {
                            return;
                        }

                        lastResponse = Environment.TickCount64;
                        pingPending = false;
                    }
                });
            }
        }
    }


    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            enabled = false;
            Monitor.PulseAll(gate);
        }
    }
}
