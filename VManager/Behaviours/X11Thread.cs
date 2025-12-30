using System;
using System.Collections.Concurrent;
using System.Threading;

namespace VManager.Behaviours;

public sealed class X11Thread : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingCollection<Action> _queue = new();
    private volatile bool _running = true;

    public X11Thread()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "X11Thread"
        };
        _thread.Start();
    }

    public void Invoke(Action action)
    {
        if (!_running) return;
        _queue.Add(action);
    }
    
    private void Run()
    {
        while (_running)
        {
            try
            {
                var action = _queue.Take();
                action();
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _queue.CompleteAdding();
        _thread.Join();
    }

}