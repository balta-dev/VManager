    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.VisualTree;

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

            // Thread X11 único para toda la app
            private readonly X11Thread _x11Thread = new X11Thread();

            private X11DragDropWindow? _current;
            private bool _active;

            private X11DragDropManager()
            {
            }

            public async Task<string?> ShowAsync(Window parentWindow)
            {
                var mainWindow = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
                if (mainWindow == null)
                {
                    Console.WriteLine("No se encontró MainWindow.");
                    return null;
                }

                var contentArea = mainWindow.FindControl<ContentControl>("ContentArea");
                if (contentArea == null)
                {
                    Console.WriteLine("No se encontró ContentArea.");
                    return null;
                }

                var currentView = contentArea.GetVisualDescendants()
                    .OfType<UserControl>()
                    .FirstOrDefault();
            
                if (currentView == null)
                {
                    Console.WriteLine("No se encontró UserControl actual.");
                    return null;
                }
            
                string viewName = currentView.GetType().Name;
                object? dataContext = currentView.DataContext;
                Console.WriteLine($"Vista actual: {viewName}");
                Console.WriteLine($"DataContext: {dataContext?.GetType().Name ?? "null"}");
                
                lock (_lock)
                {
                    if (_active)
                        return null;

                    _active = true;
                    _current = new X11DragDropWindow(_x11Thread);
                }

                try
                {
                    return await _current.ShowAndWaitForDropAsync(parentWindow, viewName, dataContext);
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