using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace VManager.Behaviors
{
    public sealed class X11DragDropManager
    {
        private static readonly object _lock = new();
        private static X11DragDropManager? _instance;

        public static X11DragDropManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new X11DragDropManager();
                }
            }
        }

        // 🔑 Thread X11 único para toda la app
        private readonly X11Thread _x11Thread = new X11Thread();

        private X11DragDropWindow? _current;
        private bool _active;

        private X11DragDropManager()
        {
        }

        public async Task<string?> ShowAsync(Window parentWindow)
        {
            lock (_lock)
            {
                if (_active)
                    return null;

                _active = true;
                _current = new X11DragDropWindow(_x11Thread);
            }

            try
            {
                return await _current.ShowAndWaitForDropAsync(parentWindow);
            }
            catch (Exception ex)
            {
                Console.WriteLine("NO SE PUDO COMPLETAR.");
            }
            finally
            {
                lock (_lock)
                {
                    _current = null;
                    _active = false;
                }
            }
            return null;
        }

        public void ForceClose()
        {
            lock (_lock)
            {
                _current?.Close();
            }
        }

        public void Shutdown()
        {
            lock (_lock)
            {
                _current?.Close();
                _current = null;
                _active = false;
            }

            _x11Thread.Dispose();
        }
    }
}