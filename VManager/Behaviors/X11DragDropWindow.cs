    using System;
    using System.Runtime.InteropServices;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Threading;
    using VManager.Behaviors;

    public class X11DragDropWindow
    {
        // P/Invoke X11
        [DllImport("libX11.so.6")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XCreateWindow(IntPtr display, IntPtr parent, int x, int y,
            uint width, uint height, uint border_width, int depth, uint @class,
            IntPtr visual, ulong valuemask, ref XSetWindowAttributes attributes);

        [DllImport("libX11.so.6")]
        private static extern int XMapWindow(IntPtr display, IntPtr window);

        [DllImport("libX11.so.6")]
        private static extern int XUnmapWindow(IntPtr display, IntPtr window);

        [DllImport("libX11.so.6")]
        private static extern int XMoveResizeWindow(IntPtr display, IntPtr window, int x, int y, uint width, uint height);

        [DllImport("libX11.so.6")]
        private static extern int XSelectInput(IntPtr display, IntPtr window, long event_mask);

        [DllImport("libX11.so.6")]
        private static extern int XNextEvent(IntPtr display, ref XEvent event_return);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport("libX11.so.6")]
        private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property,
            IntPtr type, int format, int mode, IntPtr data, int nelements);

        [DllImport("libX11.so.6")]
        private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
            long long_offset, long long_length, bool delete, IntPtr req_type,
            out IntPtr actual_type_return, out int actual_format_return,
            out ulong nitems_return, out ulong bytes_after_return, out IntPtr prop_return);

        [DllImport("libX11.so.6")]
        private static extern int XFree(IntPtr data);

        [DllImport("libX11.so.6")]
        private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
            long event_mask, ref XEvent event_send);

        [DllImport("libX11.so.6")]
        private static extern int XStoreName(IntPtr display, IntPtr window, string window_name);

        [DllImport("libX11.so.6")]
        private static extern int XFlush(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XConvertSelection(IntPtr display, IntPtr selection, IntPtr target,
            IntPtr property, IntPtr requestor, IntPtr time);

        [DllImport("libX11.so.6")]
        private static extern int XCheckTypedWindowEvent(IntPtr display, IntPtr window, int event_type, ref XEvent event_return);

        [DllImport("libX11.so.6")]
        private static extern int XPending(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XDefaultVisual(IntPtr display, int screen_number);

        [DllImport("libX11.so.6")]
        private static extern int XDefaultScreen(IntPtr display);

        [DllImport("libX11.so.6")]
        private static extern int XMatchVisualInfo(IntPtr display, int screen, int depth, int @class, out XVisualInfo vinfo_return);

        [DllImport("libX11.so.6")]
        private static extern IntPtr XCreateColormap(IntPtr display, IntPtr window, IntPtr visual, int alloc);

        [DllImport("libX11.so.6")]
        private static extern int XSetTransientForHint(IntPtr display, IntPtr window, IntPtr prop_window);

        [DllImport("libX11.so.6")]
        private static extern int XRaiseWindow(IntPtr display, IntPtr window);
        
        [DllImport("libX11.so.6")]
        private static extern int XSetWMProtocols(IntPtr display, IntPtr window, IntPtr[] protocols, int count);
        
        [DllImport("libX11.so.6")]
        private static extern IntPtr XGetAtomName(IntPtr display, IntPtr atom);
        
        [DllImport("libXfixes.so.3")]
        static extern int XFixesSetWindowShapeRegion(IntPtr display, IntPtr window, int shape, int x, int y, IntPtr region);
        
        [DllImport("libX11.so.6")]
        private static extern int XLookupString(ref XKeyEvent event_struct, 
            IntPtr buffer_return, int bytes_buffer, out IntPtr keysym_return, IntPtr status_in_out);

        // Constantes
        private const long ExposureMask = 1L << 15;
        private const long StructureNotifyMask = 1L << 17;
        private const int PropModeReplace = 0;
        private const int XA_ATOM = 4;
        private const int XA_CARDINAL = 6;
        private const int ClientMessage = 33;
        private const int SelectionNotify = 31;
        private const uint InputOutput = 1;
        private const ulong CWBackPixel = 1L << 1;
        private const ulong CWBorderPixel = 1L << 3;
        private const ulong CWColormap = 1L << 13;
        private const int AllocNone = 0;
        private const int TrueColor = 4;
        private const double Opacity = 1;
        const ulong MWM_HINTS_DECORATIONS = 1 << 1;
        const ulong MWM_DECOR_NONE = 0;
        const int ShapeInput = 2; // SHAPE_INPUT
        private const int KeyPress = 2;
        private const long KeyReleaseMask = 1L << 1;
        private const long KeyPressMask = 1L << 0;

        // Estructuras X11
        [StructLayout(LayoutKind.Explicit, Size = 192)]
        private struct XEvent
        {
            [FieldOffset(0)]
            public int type;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XClientMessageEvent
        {
            public int type;
            public IntPtr serial;
            public int send_event;
            public IntPtr display;
            public IntPtr window;
            public IntPtr message_type;
            public int format;
            public IntPtr data0;
            public IntPtr data1;
            public IntPtr data2;
            public IntPtr data3;
            public IntPtr data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XSetWindowAttributes
        {
            public IntPtr background_pixmap;
            public ulong background_pixel;
            public IntPtr border_pixmap;
            public ulong border_pixel;
            public int bit_gravity;
            public int win_gravity;
            public int backing_store;
            public ulong backing_planes;
            public ulong backing_pixel;
            public int save_under;
            public long event_mask;
            public long do_not_propagate_mask;
            public int override_redirect;
            public IntPtr colormap;
            public IntPtr cursor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XVisualInfo
        {
            public IntPtr visual;
            public IntPtr visualid;
            public int screen;
            public int depth;
            public int @class;
            public ulong red_mask;
            public ulong green_mask;
            public ulong blue_mask;
            public int colormap_size;
            public int bits_per_rgb;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct MotifWmHints
        {
            public ulong flags;
            public ulong functions;
            public ulong decorations;
            public long inputMode;
            public ulong status;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct XKeyEvent
        {
            public int type;
            public IntPtr serial;
            public int send_event;
            public IntPtr display;
            public IntPtr window;
            public IntPtr root;
            public IntPtr subwindow;
            public IntPtr time;
            public int x;
            public int y;
            public int x_root;
            public int y_root;
            public uint state;
            public uint keycode;
            public int same_screen;
        }
        
        // Variables globales
        private IntPtr display;
        private IntPtr window;
        private IntPtr xdndAware;
        private IntPtr xdndEnter;
        private IntPtr xdndLeave;
        private IntPtr xdndPosition;
        private IntPtr xdndStatus;
        private IntPtr xdndDrop;
        private IntPtr xdndFinished;
        private IntPtr xdndSelection;
        private IntPtr textUriList;
        private string? droppedFile;
        private bool isDone;
        private Window? parentWindow;
        private DispatcherTimer? positionTimer;
        private IntPtr wmDeleteWindow;
        private IntPtr wmProtocols;
        private int MarginLeft = 55;
        private int MarginRight = 20;
        private int MarginTop = 90;
        private int MarginBottom = 65;
        
        // Propiedades para almacenar el contexto actual del UserControl
        private string? currentViewName;
        private object? currentDataContext;
        
        // Propiedades públicas para acceder al estado actual
        public string? CurrentViewName => currentViewName;
        public object? CurrentDataContext => currentDataContext;
        private string GetAtomName(IntPtr atom)
        {
            if (display == IntPtr.Zero || atom == IntPtr.Zero)
                return "(null)";

            IntPtr namePtr = XGetAtomName(display, atom);
            if (namePtr == IntPtr.Zero)
                return "(null)";

            string name = Marshal.PtrToStringAnsi(namePtr) ?? "(null)";
            return name;
        }
        
        public async Task<string?> ShowAndWaitForDropAsync(Window parent)
        {
            parentWindow = parent;
            
            return await Task.Run(() =>
            {
                return ShowAndWaitForDropInternal();
            });
            
        }
        
        private string? ShowAndWaitForDropInternal()
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                Console.WriteLine("No se pudo abrir X display");
                return null;
            }

            try
            {
                IntPtr root = XDefaultRootWindow(display);
                int screen = XDefaultScreen(display);

                // Obtener visual con soporte para transparencia (32-bit ARGB)
                XVisualInfo vinfo;
                IntPtr visual;
                IntPtr colormap;

                if (XMatchVisualInfo(display, screen, 32, TrueColor, out vinfo) != 0)
                {
                    visual = vinfo.visual;
                    colormap = XCreateColormap(display, root, visual, AllocNone);
                }
                else
                {
                    // Fallback a visual por defecto
                    visual = XDefaultVisual(display, screen);
                    colormap = XCreateColormap(display, root, visual, AllocNone);
                }

                // Obtener posición y tamaño de la ventana padre
                var (x, y, width, height) = GetParentGeometry();

                // Configurar atributos de la ventana
                XSetWindowAttributes attrs = new XSetWindowAttributes
                {
                    background_pixel = 0, // 0x0000FF = Azul
                    border_pixel = 0,
                    colormap = colormap,
                    event_mask = ExposureMask | StructureNotifyMask,
                    override_redirect = 1
                };

                // Crear ventana con soporte ARGB
                window = XCreateWindow(display, root, x, y, (uint)width, (uint)height,
                    0, 32, InputOutput, visual,
                    CWBackPixel | CWBorderPixel | CWColormap, ref attrs);
                
                // Quitar decoraciones con Motif WM hints
                MotifWmHints hints = new MotifWmHints
                {
                    flags = MWM_HINTS_DECORATIONS,
                    decorations = MWM_DECOR_NONE
                };
                IntPtr hintsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(hints));
                Marshal.StructureToPtr(hints, hintsPtr, false);
                IntPtr mwmHintsAtom = XInternAtom(display, "_MOTIF_WM_HINTS", false);
                
                XChangeProperty(display, window, mwmHintsAtom, mwmHintsAtom, 32, PropModeReplace, hintsPtr, 5); 
                ////// DESCOMENTAR LA LÍNEA DE ARRIBA ////////
                
                Marshal.FreeHGlobal(hintsPtr);

                XStoreName(display, window, "VManager Drop Zone");

                // Configurar transparencia usando _NET_WM_WINDOW_OPACITY
                IntPtr netWmWindowOpacity = XInternAtom(display, "_NET_WM_WINDOW_OPACITY", false);
                uint opacity = (uint)(Opacity * 0xFFFFFFFF); // 30% opacidad
                IntPtr opacityPtr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(opacityPtr, (int)opacity);
                XChangeProperty(display, window, netWmWindowOpacity, XA_CARDINAL, 32, PropModeReplace, opacityPtr, 1);
                Marshal.FreeHGlobal(opacityPtr);

                // Configurar XDND
                xdndAware = XInternAtom(display, "XdndAware", false);
                xdndEnter = XInternAtom(display, "XdndEnter", false);
                xdndLeave = XInternAtom(display, "XdndLeave", false);
                xdndPosition = XInternAtom(display, "XdndPosition", false);
                xdndStatus = XInternAtom(display, "XdndStatus", false);
                xdndDrop = XInternAtom(display, "XdndDrop", false);
                xdndFinished = XInternAtom(display, "XdndFinished", false);
                xdndSelection = XInternAtom(display, "XdndSelection", false);
                textUriList = XInternAtom(display, "text/uri-list", false);

                int version = 5;
                IntPtr versionPtr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(versionPtr, version);
                XChangeProperty(display, window, xdndAware, XA_ATOM, 32, PropModeReplace, versionPtr, 1);
                Marshal.FreeHGlobal(versionPtr);

                // Hacer que la ventana sea "utility" para que no aparezca en taskbar
                IntPtr netWmWindowType = XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
                IntPtr netWmWindowTypeUtility = XInternAtom(display, "_NET_WM_WINDOW_TYPE_UTILITY", false);
                IntPtr typePtr = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(typePtr, netWmWindowTypeUtility);
                XChangeProperty(display, window, netWmWindowType, XA_ATOM, 32, PropModeReplace, typePtr, 1);
                Marshal.FreeHGlobal(typePtr);

                // Configurar como "always on top"
                IntPtr netWmState = XInternAtom(display, "_NET_WM_STATE", false);
                IntPtr netWmStateAbove = XInternAtom(display, "_NET_WM_STATE_ABOVE", false);
                IntPtr statePtr = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(statePtr, netWmStateAbove);
                XChangeProperty(display, window, netWmState, XA_ATOM, 32, PropModeReplace, statePtr, 1);
                Marshal.FreeHGlobal(statePtr);

                // Configurar como ventana transient del padre (esto la mantiene encima del padre)
                if (parentWindow?.TryGetPlatformHandle()?.Handle is IntPtr parentHandle && parentHandle != IntPtr.Zero)
                {
                    XSetTransientForHint(display, window, parentHandle);
                }

                XSelectInput(display, window, ExposureMask | StructureNotifyMask | KeyPressMask);
                XMapWindow(display, window);
                
                // Hacemos que toda la ventana sea input-transparent
                XFixesSetWindowShapeRegion(display, window, ShapeInput, 0, 0, IntPtr.Zero);
                
                // Registrar protocolo WM_DELETE_WINDOW
                wmProtocols = XInternAtom(display, "WM_PROTOCOLS", false);
                wmDeleteWindow = XInternAtom(display, "WM_DELETE_WINDOW", false);
                XSetWMProtocols(display, window, new IntPtr[] { wmDeleteWindow }, 1);
                
                // Asegurar que esté arriba
                XRaiseWindow(display, window);
                XFlush(display);

                // Iniciar timer para seguir la ventana padre
                StartPositionTracking();

                ProcessEvents();

                StopPositionTracking();

                return droppedFile;
            }
            finally
            {
                if (display != IntPtr.Zero)
                    XCloseDisplay(display);
            }
        }

        private (int x, int y, int width, int height) GetParentGeometry()
        {
            if (parentWindow == null)
                return (100, 100, 900, 695);

            int x = 0, y = 0, width = 900, height = 695;

            Dispatcher.UIThread.Post(() =>
            {
                var pos = parentWindow.Position;
                var size = parentWindow.ClientSize;
                
                x = pos.X;
                y = pos.Y;
                width = (int)size.Width;
                height = (int)size.Height;
            });

            System.Threading.Thread.Sleep(50); // Esperar a que se actualicen los valores
            return (x, y, width, height);
        }

        private void StartPositionTracking()
        {
            if (parentWindow == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                positionTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // 60FPS -> 16*16 ~= 3.75 FPS
                };

                positionTimer.Tick += (s, e) =>
                {
                    if (parentWindow == null || display == IntPtr.Zero || window == IntPtr.Zero)
                    {
                        positionTimer?.Stop();
                        return;
                    }

                    var pos = parentWindow.Position;
                    var size = parentWindow.ClientSize;

                    if (parentWindow.FindControl<SplitView>("MainSplitView") is SplitView sv)
                        MarginLeft = sv.IsPaneOpen ? (int)sv.OpenPaneLength : (int)sv.CompactPaneLength;

                    // Obtener bounds de la pantalla actual del padre
                    var screen = parentWindow.Screens.ScreenFromWindow(parentWindow);
                    var screenBounds = screen.WorkingArea;

                    // Calcular posición y tamaño deseados
                    int desiredX = pos.X + MarginLeft;
                    int desiredY = pos.Y + MarginTop;
                    int desiredWidth = (int)size.Width - MarginLeft - MarginRight;
                    int desiredHeight = (int)size.Height - MarginTop - MarginBottom;

                    // Clampear posición (todos los bordes)
                    int x = Math.Clamp(desiredX, screenBounds.X, screenBounds.Right);
                    int y = Math.Clamp(desiredY, screenBounds.Y, screenBounds.Bottom);

                    // Ajustar ancho considerando desplazamiento a la izquierda
                    int widthLostLeft = Math.Max(0, screenBounds.X - desiredX);
                    uint width = (uint)Math.Max(0, Math.Min(
                        desiredWidth - widthLostLeft,
                        screenBounds.Right - x
                    ));

                    // Ajustar alto considerando desplazamiento arriba
                    int heightLostTop = Math.Max(0, screenBounds.Y - desiredY);
                    uint height = (uint)Math.Max(0, Math.Min(
                        desiredHeight - heightLostTop,
                        screenBounds.Bottom - y - 31
                    ));
                    
                    /*Console.WriteLine($"pos.Y: {pos.Y}");
                    Console.WriteLine($"size.Height: {size.Height}");
                    Console.WriteLine($"desiredY: {desiredY}");
                    Console.WriteLine($"desiredHeight: {desiredHeight}");
                    Console.WriteLine($"y (final): {y}");
                    Console.WriteLine($"height (final): {height}");
                    Console.WriteLine($"---"); */

                    // Actualizar en X11
                    Task.Run(() =>
                    {
                        try
                        {
                            XMoveResizeWindow(display, window, x, y, width, height);
                            XRaiseWindow(display, window);
                            XFlush(display);
                        }
                        catch { }
                    });
                };

                positionTimer.Start();
            });
        }


        private void StopPositionTracking()
        {
            if (positionTimer != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    positionTimer?.Stop();
                    positionTimer = null;
                });
            }
        }

        private void ProcessEvents()
        {
            XEvent ev = new XEvent();
            IntPtr sourceWindow = IntPtr.Zero;

            while (!isDone)
            {
                if (XPending(display) > 0)
                {
                    int result = XNextEvent(display, ref ev);
                    if (result != 0) break;

                    if (ev.type == KeyPress)
                    {
                        IntPtr evPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XEvent)));
                        Marshal.StructureToPtr(ev, evPtr, false);
                        XKeyEvent keyEvent = Marshal.PtrToStructure<XKeyEvent>(evPtr)!;
                        Marshal.FreeHGlobal(evPtr);
                
                        IntPtr keysym;
                        IntPtr buffer = Marshal.AllocHGlobal(32);
                        XLookupString(ref keyEvent, buffer, 32, out keysym, IntPtr.Zero);
                        Marshal.FreeHGlobal(buffer);
                
                        // 0xff1b es el keysym de ESC
                        if (keysym.ToInt64() == 0xff1b || keysym.ToInt64() == 0xFF1B)
                        {
                            Console.WriteLine("ESC detectado en X11 - cerrando ventana");
                            isDone = true;
                            droppedFile = null; // Cancelar drop
                            break;
                        }
                    }
                    
                    if (ev.type == ClientMessage)
                    {
                        IntPtr evPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XEvent)));
                        Marshal.StructureToPtr(ev, evPtr, false);
                        XClientMessageEvent clientMsg = Marshal.PtrToStructure<XClientMessageEvent>(evPtr)!;
                        Marshal.FreeHGlobal(evPtr);

                        Console.WriteLine($"wmDeleteWindow atom: {wmDeleteWindow}");
                        Console.WriteLine($"clientMsg.message_type: {clientMsg.message_type}");
                        string atomName = GetAtomName(clientMsg.message_type);
                        Console.WriteLine($"ClientMessage recibido: {clientMsg.message_type} -> {atomName}");
                        
                        if (clientMsg.message_type == wmProtocols) // wmProtocols = XInternAtom(display, "WM_PROTOCOLS", false)
                        {
                            // data0 contiene el átomo del protocolo solicitado
                            IntPtr protocol = clientMsg.data0;
                            if (protocol == wmDeleteWindow) 
                            {
                                Close(); // cerrar solo la ventana overlay
                            }
                        }

                        if (clientMsg.message_type == xdndEnter)
                        {
                            sourceWindow = clientMsg.data0;
                            X11DragFeedback.NotifyDragEnter(true);
                        }
                        else if (clientMsg.message_type == xdndPosition)
                        {
                            SendXdndStatus(sourceWindow);
                        }
                        else if (clientMsg.message_type == xdndDrop)
                        {
                            HandleDrop(sourceWindow, clientMsg);
                        }
                        else if (clientMsg.message_type == xdndLeave) // Agregá este átomo en el constructor
                        {
                            X11DragFeedback.NotifyDragLeave();
                        }
                    }
                }
                else
                {
                    System.Threading.Thread.Sleep(10);
                }
            }
        }

        private void SendXdndStatus(IntPtr sourceWindow)
        {
            XEvent ev = new XEvent { type = ClientMessage };
            IntPtr evPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XEvent)));
            Marshal.StructureToPtr(ev, evPtr, false);

            XClientMessageEvent response = new XClientMessageEvent
            {
                type = ClientMessage,
                display = display,
                window = sourceWindow,
                message_type = xdndStatus,
                format = 32,
                data0 = window,
                data1 = new IntPtr(1) // aceptamos
            };

            Marshal.StructureToPtr(response, evPtr, false);
            XEvent statusEvent = Marshal.PtrToStructure<XEvent>(evPtr)!;
            Marshal.FreeHGlobal(evPtr);

            XSendEvent(display, sourceWindow, false, 0, ref statusEvent);
            XFlush(display);
        }

        private void HandleDrop(IntPtr sourceWindow, XClientMessageEvent clientMsg)
        {
            IntPtr xdndActionCopy = XInternAtom(display, "XdndActionCopy", false);

            XConvertSelection(display, xdndSelection, textUriList, xdndSelection, window, IntPtr.Zero);
            XFlush(display);

            XEvent selEvent = new XEvent();
            bool gotSelection = false;

            for (int i = 0; i < 100; i++)
            {
                if (XCheckTypedWindowEvent(display, window, SelectionNotify, ref selEvent) != 0)
                {
                    gotSelection = true;
                    break;
                }
                System.Threading.Thread.Sleep(10);
            }

            bool dropSuccess = false;
    
            if (gotSelection)
            {
                IntPtr actualType, propData;
                int actualFormat;
                ulong nItems, bytesAfter;

                int result = XGetWindowProperty(display, window, xdndSelection,
                    0, 65536, false, textUriList,
                    out actualType, out actualFormat, out nItems, out bytesAfter, out propData);

                if (result == 0 && propData != IntPtr.Zero && nItems > 0)
                {
                    string data = Marshal.PtrToStringAnsi(propData, (int)nItems) ?? "";
                    XFree(propData);

                    var uris = data.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    var firstUri = uris.FirstOrDefault();

                    if (!string.IsNullOrEmpty(firstUri))
                    {
                        if (firstUri.StartsWith("file://"))
                            droppedFile = Uri.UnescapeDataString(firstUri.Substring(7));
                        else if (firstUri.StartsWith("'file://") && firstUri.EndsWith("'"))
                            droppedFile = Uri.UnescapeDataString(firstUri.Substring(8, firstUri.Length - 9));
                        else
                            droppedFile = firstUri;
                
                        // Validar extensión del archivo
                        string ext = System.IO.Path.GetExtension(droppedFile).ToLowerInvariant();
                
                        // Acá validás según tu lógica (video/audio/txt)
                        bool isValid = IsFileValid(ext);
                
                        if (isValid)
                        {
                            dropSuccess = true;
                            X11DragFeedback.NotifyDropSuccess();
                        }
                        else
                        {
                            droppedFile = null;
                            X11DragFeedback.NotifyDropError();
                        }
                    }
                }
            }
    
            if (!dropSuccess && droppedFile == null)
            {
                X11DragFeedback.NotifyDropError();
            }


            // Enviar XdndFinished
            XEvent ev = new XEvent { type = ClientMessage };
            IntPtr evPtr2 = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(XEvent)));

            XClientMessageEvent finished = new XClientMessageEvent
            {
                type = ClientMessage,
                display = display,
                window = sourceWindow,
                message_type = xdndFinished,
                format = 32,
                data0 = window,
                data1 = new IntPtr(1),
                data2 = xdndActionCopy
            };

            Marshal.StructureToPtr(finished, evPtr2, false);
            XEvent finishedEvent = Marshal.PtrToStructure<XEvent>(evPtr2)!;
            Marshal.FreeHGlobal(evPtr2);

            XSendEvent(display, sourceWindow, false, 0, ref finishedEvent);
            XFlush(display);

            System.Threading.Thread.Sleep(200);
            isDone = true;
        }

        public void Close()
        {
            isDone = true;
            StopPositionTracking();
            
            if (display != IntPtr.Zero && window != IntPtr.Zero)
            {
                XUnmapWindow(display, window);
                XFlush(display);
            }
        }
        
        // Método helper para validar archivos
        private bool IsFileValid(string extension)
        {
            // Acá ponés tu lógica según el contexto actual
            // Podés usar ReadParentContext() para determinar qué aceptar
    
            string[] validExtensions;
    
            // Si es Herramienta que acepta TXT
            if (currentViewName == "ConfiguracionView")
            {
                validExtensions = new[] { ".txt" };
            }
            else
            {
                // Videos y audios
                validExtensions = new[] 
                { 
                    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".3gp",
                    ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
                };
            }
    
            return validExtensions.Contains(extension);
        }
    }