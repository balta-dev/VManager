using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace VManager.Splash;

/// <summary>
/// Splash screen para Linux usando X11 + libpng puro.
/// Sin dependencias externas más allá de las que ya tiene cualquier sistema Linux con X.
/// </summary>
internal static class X11Splash
{
    private const int WIN_W = 900;
    private const int WIN_H = 220;

    // ── Animación indeterminada ───────────────────────────────────────────────
    private static float _barPos  = 0f;
    private static float _barDir  = 1f;
    private const  float BAR_BLOCK = 0.25f;
    private const  float BAR_SPEED = 0.55f;
    private const  int   PROGRESS_H = 3;

    public static void Run(string imagePath, ref bool shouldClose, ref bool isReady)
    {
        // ── Conectar a X11 ────────────────────────────────────────────────────
        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            Console.Error.WriteLine("[SPLASH] No se pudo conectar a X11");
            isReady = true;
            return;
        }

        var screen = XDefaultScreen(display);
        var root   = XDefaultRootWindow(display);

        // ── Obtener tamaño de pantalla para centrar ───────────────────────────
        int screenW = XDisplayWidth(display,  screen);
        int screenH = XDisplayHeight(display, screen);
        int x = (screenW - WIN_W) / 2;
        int y = (screenH - WIN_H) / 2;

        // ── Crear ventana sin decoraciones ────────────────────────────────────
        var setWA = new XSetWindowAttributes
        {
            background_pixel  = 0x001C1C1C,
            border_pixel      = 0x00696969,
            override_redirect = true,  // sin decoraciones del WM
        };

        ulong cwMask = CWBackPixel | CWBorderPixel | CWOverrideRedirect;
        var window = XCreateWindow(
            display, root,
            x, y, (uint)WIN_W, (uint)WIN_H, 1,
            CopyFromParent, InputOutput, IntPtr.Zero,
            cwMask, ref setWA);

        // ── Hints para quitar decoraciones (por si override_redirect no alcanza) ──
        SetMotifHints(display, window);

        // ── Seleccionar eventos ───────────────────────────────────────────────
        XSelectInput(display, window, ExposureMask | StructureNotifyMask);
        XMapWindow(display, window);
        XFlush(display);

        // ── GC para dibujo ────────────────────────────────────────────────────
        var gcValues = new XGCValues();
        var gc = XCreateGC(display, window, 0, ref gcValues);

        // ── Cargar imagen PNG → XImage ────────────────────────────────────────
        IntPtr ximage  = IntPtr.Zero;
        int    imgW    = 0, imgH = 0;
        byte[]? pixels = null;

        if (File.Exists(imagePath))
            (pixels, imgW, imgH) = LoadPngSoftware(imagePath);

        if (pixels != null && imgW > 0)
            ximage = CreateXImage(display, screen, pixels, imgW, imgH);

        isReady = true; // desbloquear NativeSplash.Show()

        // ── Primer dibujado (para evitar iniciar en negro) ────────────────────
        Render(display, window, gc, ximage, imgW, imgH);
        RenderProgress(display, window, gc);
        XFlush(display);

        var lastTick = DateTime.UtcNow;
        var ev       = new XEvent();

        while (!shouldClose)
        {
            bool needsFullRedraw = false;

            // Procesar eventos pendientes sin bloquear
            while (XPending(display) > 0)
            {
                XNextEvent(display, ref ev);
                if (ev.type == Expose && ev.xexpose.count == 0)
                {
                    needsFullRedraw = true; // Solo redibujar imagen/texto si el WM lo pide
                }
            }

            // Avanzar animación
            var now = DateTime.UtcNow;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick  = now;

            _barPos += _barDir * BAR_SPEED * dt;
            if (_barPos + BAR_BLOCK >= 1f) { _barPos = 1f - BAR_BLOCK; _barDir = -1f; }
            if (_barPos <= 0f)             { _barPos = 0f;              _barDir =  1f; }

            // Redibujar el contenido estático solo si hubo un evento Expose
            if (needsFullRedraw)
            {
                Render(display, window, gc, ximage, imgW, imgH);
            }

            // La barra de progreso se redibuja en cada frame de forma localizada
            RenderProgress(display, window, gc);
            XFlush(display);

            Thread.Sleep(16);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────
        if (ximage != IntPtr.Zero) XDestroyImage(ximage);
        XFreeGC(display, gc);
        XDestroyWindow(display, window);
        XCloseDisplay(display);
    }

    // ── Render estático (Imagen y Textos) ─────────────────────────────────────
    private static void Render(IntPtr display, IntPtr window, IntPtr gc,
                               IntPtr ximage, int imgW, int imgH)
    {
        // Fondo
        XSetForeground(display, gc, 0x001C1C1C);
        XFillRectangle(display, window, gc, 0, 0, WIN_W, WIN_H - PROGRESS_H); // No sobreescribir el área de la barra

        // Borde DimGray
        XSetForeground(display, gc, 0x00696969);
        XDrawRectangle(display, window, gc, 0, 0, WIN_W - 1, WIN_H - 1);
        
        // Imagen centrada
        if (ximage != IntPtr.Zero)
        {
            int reservedBottom = 38;
            int availH = WIN_H - reservedBottom - 20;
            int availW = WIN_W - 40;

            int drawW = imgW, drawH = imgH;
            if (drawW > availW || drawH > availH)
            {
                float scale = Math.Min((float)availW / drawW, (float)availH / drawH);
                drawW = (int)(drawW * scale);
                drawH = (int)(drawH * scale);
            }

            int imgX = (WIN_W - drawW) / 2;
            int imgY = 20 + (availH - drawH) / 2;
            
            // Escalar si es necesario: crear imagen escalada
            if (drawW != imgW || drawH != imgH)
            {
                var scaled = ScaleXImage(display, ximage, imgW, imgH, drawW, drawH);
                if (scaled != IntPtr.Zero)
                {
                    XPutImage(display, window, gc, scaled, 0, 0, imgX, imgY, (uint)drawW, (uint)drawH);
                    XDestroyImage(scaled);
                }
            }
            else
            {
                XPutImage(display, window, gc, ximage, 0, 0, imgX, imgY, (uint)imgW, (uint)imgH);
            }
        }

        // Texto "Iniciando..." (X11 básico, sin antialiasing)
        XSetForeground(display, gc, 0x00999999);
        var text  = "Iniciando...";
        int textX = (WIN_W - text.Length * 6) / 2;
        int textY = WIN_H - 18;
        XDrawString(display, window, gc, textX, textY, text, text.Length);
    }

    // ── Render dinámico (Solo Barra de Progreso) ──────────────────────────────
    private static void RenderProgress(IntPtr display, IntPtr window, IntPtr gc)
    {
        int barY      = WIN_H - PROGRESS_H;
        int barTotalW = WIN_W;
        int blockW    = (int)(barTotalW * BAR_BLOCK);
        int blockX    = (int)(barTotalW * _barPos);

        // Fondo de la barra (limpia el rastro del frame anterior)
        XSetForeground(display, gc, 0x00302050);
        XFillRectangle(display, window, gc, 1, barY, WIN_W - 2, PROGRESS_H - 1); // Margen para no pisar el borde de la ventana

        // Bloque de progreso
        XSetForeground(display, gc, 0x0066BBFF);
        XFillRectangle(display, window, gc, blockX, barY, blockW, PROGRESS_H - 1);
    }
    
    private static void SplashLog(string msg)
    {
        var path = Path.Combine(Path.GetTempPath(), "vmanager_splash_debug.log");
        File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
    }

    // ── Cargar PNG sin libpng (usando software decoder mínimo) ───────────────
    private static (byte[]? pixels, int w, int h) LoadPngSoftware(string path)
    {
        // Intentar con libpng
        try { return LoadWithLibpng(path); }
        catch (Exception ex)
        {
            SplashLog($"LoadPngSoftware falló: {ex.GetType().Name}: {ex.Message}");
            SplashLog($"StackTrace: {ex.StackTrace}");
        }

        // Fallback: no imagen
        return (null, 0, 0);
    }

    private static (byte[]? pixels, int w, int h) LoadWithLibpng(string path)
    {
        SplashLog($"LoadWithLibpng: abriendo {path}");
        var fp = fopen(path, "rb");
        SplashLog($"fopen: {fp}");
        if (fp == IntPtr.Zero) return (null, 0, 0);

        try
        {
            var sig = new byte[8];
            fread(sig, 1, 8, fp);
            int sigCmp = png_sig_cmp(sig, 0, 8);
            SplashLog($"png_sig_cmp: {sigCmp}");
            if (sigCmp != 0) return (null, 0, 0);

            var verPtr = png_get_libpng_ver(IntPtr.Zero);
            var version = Marshal.PtrToStringAnsi(verPtr)!;
            SplashLog($"libpng version: {version}");
            
            var png = png_create_read_struct(version, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            SplashLog($"png_create_read_struct: {png}");
            if (png == IntPtr.Zero) return (null, 0, 0);

            var info = png_create_info_struct(png);
            SplashLog($"png_create_info_struct: {info}");
            if (info == IntPtr.Zero) { png_destroy_read_struct(ref png, IntPtr.Zero, IntPtr.Zero); return (null, 0, 0); }

            png_init_io(png, fp);
            png_set_sig_bytes(png, 8);
            png_read_info(png, info);

            int w = (int)png_get_image_width(png, info);
            int h = (int)png_get_image_height(png, info);
            SplashLog($"imagen: {w}x{h}");

            // Forzar RGBA
            png_set_expand(png);
            png_set_filler(png, 0xFF, 1 /*AFTER*/);
            png_set_gray_to_rgb(png);
            png_read_update_info(png, info);

            var pixels = new byte[w * h * 4];
            var rowPtrs = new IntPtr[h];
            var gcHandles = new GCHandle[h];

            for (int i = 0; i < h; i++)
            {
                var rowSlice = new ArraySegment<byte>(pixels, i * w * 4, w * 4);
                gcHandles[i] = GCHandle.Alloc(rowSlice.Array, GCHandleType.Pinned);
                rowPtrs[i]   = gcHandles[i].AddrOfPinnedObject() + i * w * 4;
            }

            png_read_image(png, rowPtrs);

            foreach (var h2 in gcHandles) h2.Free();
            png_destroy_read_struct(ref png, ref info, IntPtr.Zero);

            return (pixels, w, h);
        }
        finally
        {
            fclose(fp);
        }
    }

    // ── Crear XImage desde pixels RGBA ───────────────────────────────────────
    private static IntPtr CreateXImage(IntPtr display, int screen, byte[] pixels, int w, int h)
    {
        // Color de fondo #1c1c1c
        const byte bgR = 0x1C, bgG = 0x1C, bgB = 0x1C;

        var data = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            byte r = pixels[i * 4 + 0];
            byte g = pixels[i * 4 + 1];
            byte b = pixels[i * 4 + 2];
            byte a = pixels[i * 4 + 3];

            // Alpha blending contra el fondo
            float alpha = a / 255f;
            data[i * 4 + 0] = (byte)(b * alpha + bgB * (1 - alpha)); // B
            data[i * 4 + 1] = (byte)(g * alpha + bgG * (1 - alpha)); // G
            data[i * 4 + 2] = (byte)(r * alpha + bgR * (1 - alpha)); // R
            data[i * 4 + 3] = 0xFF;
        }

        var dataPtr = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, dataPtr, data.Length);

        var visual = XDefaultVisual(display, screen);
        var depth  = XDefaultDepth(display, screen);
        return XCreateImage(display, visual, (uint)depth, ZPixmap, 0,
            dataPtr, (uint)w, (uint)h, 32, 0);
    }

    // ── Escalar XImage (nearest neighbor) ────────────────────────────────────
    private static IntPtr ScaleXImage(IntPtr display, IntPtr src,
                                      int srcW, int srcH, int dstW, int dstH)
    {
        var srcPixels = new int[srcW * srcH];
        for (int sy = 0; sy < srcH; sy++)
        for (int sx = 0; sx < srcW; sx++)
            srcPixels[sy * srcW + sx] = (int)XGetPixel(src, sx, sy);

        var dstData = new byte[dstW * dstH * 4];
        for (int dy = 0; dy < dstH; dy++)
        for (int dx = 0; dx < dstW; dx++)
        {
            int sx = dx * srcW / dstW;
            int sy = dy * srcH / dstH;
            int px = srcPixels[sy * srcW + sx];
            dstData[(dy * dstW + dx) * 4 + 0] = (byte)(px & 0xFF);
            dstData[(dy * dstW + dx) * 4 + 1] = (byte)((px >> 8) & 0xFF);
            dstData[(dy * dstW + dx) * 4 + 2] = (byte)((px >> 16) & 0xFF);
            dstData[(dy * dstW + dx) * 4 + 3] = 0xFF;
        }

        var dataPtr = Marshal.AllocHGlobal(dstData.Length);
        Marshal.Copy(dstData, 0, dataPtr, dstData.Length);

        var screen  = XDefaultScreen(display);
        var visual  = XDefaultVisual(display, screen);
        var depth   = XDefaultDepth(display, screen);
        return XCreateImage(display, visual, (uint)depth, ZPixmap, 0,
                            dataPtr, (uint)dstW, (uint)dstH, 32, 0);
    }

    // ── Motif hints para quitar decoraciones ──────────────────────────────────
    private static void SetMotifHints(IntPtr display, IntPtr window)
    {
        var atom = XInternAtom(display, "_MOTIF_WM_HINTS", false);
        if (atom == IntPtr.Zero) return;

        long[] hints = { 2, 0, 0, 0, 0 }; // MWM_HINTS_DECORATIONS = 2, decorations = 0
        var ptr = Marshal.AllocHGlobal(hints.Length * sizeof(long));
        Marshal.Copy(hints, 0, ptr, hints.Length);
        XChangeProperty(display, window, atom, atom, 32,
                        PropModeReplace, ptr, 5);
        Marshal.FreeHGlobal(ptr);
    }

    // ── Constantes X11 ───────────────────────────────────────────────────────
    private const ulong CWBackPixel      = 1 << 1;
    private const ulong CWBorderPixel    = 1 << 3;
    private const ulong CWOverrideRedirect = 1 << 9;
    private const int   CopyFromParent   = 0;
    private const int   InputOutput      = 1;
    private const long  ExposureMask     = 1 << 15;
    private const long  StructureNotifyMask = 1 << 17;
    private const int   Expose           = 12;
    private const int   ZPixmap          = 2;
    private const int   PropModeReplace  = 0;

    // ── P/Invoke X11 ─────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct XSetWindowAttributes
    {
        public IntPtr background_pixmap;
        public ulong  background_pixel;
        public IntPtr border_pixmap;
        public ulong  border_pixel;
        public int    bit_gravity, win_gravity, backing_store;
        public ulong  backing_planes, backing_pixel;
        public bool   save_under;
        public long   event_mask, do_not_propagate_mask;
        public bool   override_redirect;
        public IntPtr colormap;
        public IntPtr cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XGCValues { public int function; public ulong plane_mask, foreground, background; }

    [StructLayout(LayoutKind.Sequential)]
    private struct XExposeEvent { public int type; public ulong serial; public bool send_event; public IntPtr display; public IntPtr window; public int x, y, width, height, count; }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    private struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XExposeEvent xexpose;
    }

    [DllImport("libX11.so.6")] static extern IntPtr XOpenDisplay(string? display);
    [DllImport("libX11.so.6")] static extern int    XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] static extern int    XDefaultScreen(IntPtr display);
    [DllImport("libX11.so.6")] static extern IntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] static extern int    XDisplayWidth(IntPtr display, int screen);
    [DllImport("libX11.so.6")] static extern int    XDisplayHeight(IntPtr display, int screen);
    [DllImport("libX11.so.6")] static extern IntPtr XCreateWindow(IntPtr display, IntPtr parent, int x, int y, uint w, uint h, uint border, int depth, uint cls, IntPtr visual, ulong mask, ref XSetWindowAttributes attrs);
    [DllImport("libX11.so.6")] static extern int    XMapWindow(IntPtr display, IntPtr window);
    [DllImport("libX11.so.6")] static extern int    XDestroyWindow(IntPtr display, IntPtr window);
    [DllImport("libX11.so.6")] static extern int    XFlush(IntPtr display);
    [DllImport("libX11.so.6")] static extern int    XPending(IntPtr display);
    [DllImport("libX11.so.6")] static extern int    XNextEvent(IntPtr display, ref XEvent ev);
    [DllImport("libX11.so.6")] static extern IntPtr XCreateGC(IntPtr display, IntPtr drawable, ulong mask, ref XGCValues values);
    [DllImport("libX11.so.6")] static extern int    XFreeGC(IntPtr display, IntPtr gc);
    [DllImport("libX11.so.6")] static extern int    XSetForeground(IntPtr display, IntPtr gc, ulong color);
    [DllImport("libX11.so.6")] static extern int    XFillRectangle(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, int w, int h);
    [DllImport("libX11.so.6")] static extern int    XDrawRectangle(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, int w, int h);
    [DllImport("libX11.so.6")] static extern int    XDrawString(IntPtr display, IntPtr drawable, IntPtr gc, int x, int y, string str, int len);
    [DllImport("libX11.so.6")] static extern int    XPutImage(IntPtr display, IntPtr drawable, IntPtr gc, IntPtr image, int srcX, int srcY, int dstX, int dstY, uint w, uint h);
    [DllImport("libX11.so.6")] static extern int    XDestroyImage(IntPtr image);
    [DllImport("libX11.so.6")] static extern ulong  XGetPixel(IntPtr image, int x, int y);
    [DllImport("libX11.so.6")] static extern IntPtr XCreateImage(IntPtr display, IntPtr visual, uint depth, int format, int offset, IntPtr data, uint w, uint h, int pad, int bytesPerLine);
    [DllImport("libX11.so.6")] static extern IntPtr XDefaultVisual(IntPtr display, int screen);
    [DllImport("libX11.so.6")] static extern int    XDefaultDepth(IntPtr display, int screen);
    [DllImport("libX11.so.6")] static extern IntPtr XSelectInput(IntPtr display, IntPtr window, long mask);
    [DllImport("libX11.so.6")] static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
    [DllImport("libX11.so.6")] static extern int    XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int nElements);

    // libpng
    [DllImport("libpng16.so.16")] static extern IntPtr png_create_read_struct(string ver, IntPtr err, IntPtr errFn, IntPtr warnFn);
    [DllImport("libpng16.so.16")] static extern IntPtr png_create_info_struct(IntPtr png);
    [DllImport("libpng16.so.16")] static extern void   png_destroy_read_struct(ref IntPtr png, IntPtr info, IntPtr end);
    [DllImport("libpng16.so.16")] static extern void   png_destroy_read_struct(ref IntPtr png, ref IntPtr info, IntPtr end);
    [DllImport("libpng16.so.16")] static extern void   png_init_io(IntPtr png, IntPtr fp);
    [DllImport("libpng16.so.16")] static extern void   png_set_sig_bytes(IntPtr png, int num);
    [DllImport("libpng16.so.16")] static extern void   png_read_info(IntPtr png, IntPtr info);
    [DllImport("libpng16.so.16")] static extern uint   png_get_image_width(IntPtr png, IntPtr info);
    [DllImport("libpng16.so.16")] static extern uint   png_get_image_height(IntPtr png, IntPtr info);
    [DllImport("libpng16.so.16")] static extern void   png_set_expand(IntPtr png);
    [DllImport("libpng16.so.16")] static extern void   png_set_filler(IntPtr png, uint filler, int loc);
    [DllImport("libpng16.so.16")] static extern void   png_set_gray_to_rgb(IntPtr png);
    [DllImport("libpng16.so.16")] static extern void   png_read_update_info(IntPtr png, IntPtr info);
    [DllImport("libpng16.so.16")] static extern void   png_read_image(IntPtr png, IntPtr[] rows);
    [DllImport("libpng16.so.16")] static extern int    png_sig_cmp(byte[] sig, int start, int num);
   
    [DllImport("libpng16.so.16")]
    private static extern IntPtr png_get_libpng_ver(IntPtr png_ptr);

    // libc
    [DllImport("libc.so.6", CharSet = CharSet.Ansi)] static extern IntPtr fopen(string path, string mode);
    [DllImport("libc.so.6")] static extern int    fclose(IntPtr fp);
    [DllImport("libc.so.6")] static extern ulong  fread(byte[] buf, ulong size, ulong count, IntPtr fp);
}