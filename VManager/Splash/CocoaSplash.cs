using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace VManager.Splash;

/// <summary>
/// Splash screen para macOS usando el Objective-C runtime directamente.
/// Sin AppKit managed binding — solo P/Invoke a libobjc.
/// Paint separado estático/dinámico para evitar titilado.
/// </summary>
internal static class CocoaSplash
{
    private const int WIN_W = 900;
    private const int WIN_H = 220;
    private const int PROGRESS_H = 3;

    private static float _barPos  = 0f;
    private static float _barDir  = 1f;
    private const  float BAR_BLOCK = 0.25f;
    private const  float BAR_SPEED = 0.55f;

    private static IntPtr _nsImage = IntPtr.Zero;
    private static int    _imgW, _imgH;

    public static void Run(string imagePath, ref bool shouldClose, ref bool isReady)
    {
        // ── NSApplication ─────────────────────────────────────────────────────
        var nsApp = objc_getClass("NSApplication");
        var app   = objc_msgSend_ret(nsApp, sel("sharedApplication"));
        objc_msgSend_long(app, sel("setActivationPolicy:"), 1L); // Accessory: sin icono en Dock

        // ── Crear ventana borderless ──────────────────────────────────────────
        var allocedWin = objc_msgSend_ret(objc_getClass("NSWindow"), sel("alloc"));
        var rect       = new NSRect { x = 0, y = 0, width = WIN_W, height = WIN_H };
        var window     = objc_msgSend_initWindow(allocedWin,
            sel("initWithContentRect:styleMask:backing:defer:"),
            rect, 0u, 2u, false);

        objc_msgSend_ptr(window, sel("center"));

        // Fondo #1c1c1c
        var bgColor = objc_msgSend_ret_4d(objc_getClass("NSColor"),
            sel("colorWithCalibratedRed:green:blue:alpha:"),
            0.110, 0.110, 0.110, 1.0);
        objc_msgSend_ptr(window, sel("setBackgroundColor:"), bgColor);
        objc_msgSend_bool(window, sel("setOpaque:"), true);
        objc_msgSend_long(window, sel("setLevel:"), 8L); // NSFloatingWindowLevel

        // ── Cargar imagen ─────────────────────────────────────────────────────
        if (File.Exists(imagePath))
        {
            var allocImg = objc_msgSend_ret(objc_getClass("NSImage"), sel("alloc"));
            _nsImage = objc_msgSend_ret_ptr(allocImg,
                sel("initWithContentsOfFile:"), MakeNSString(imagePath));

            if (_nsImage != IntPtr.Zero)
            {
                var size = objc_msgSend_size(_nsImage, sel("size"));
                _imgW = (int)size.width;
                _imgH = (int)size.height;
            }
        }

        var contentView = objc_msgSend_ret(window, sel("contentView"));

        // Mostrar ventana
        objc_msgSend_ptr(window, sel("makeKeyAndOrderFront:"), IntPtr.Zero);
        objc_msgSend_bool(app, sel("activateIgnoringOtherApps:"), true);

        // ── Dibujo inicial del contenido estático ─────────────────────────────
        // Evita que la ventana aparezca en negro un frame antes de la imagen
        DrawStatic(contentView);

        isReady = true;

        var nsDate   = objc_getClass("NSDate");
        var lastTick = DateTime.UtcNow;
        var needsStaticRedraw = false;

        while (!shouldClose)
        {
            // Drenar eventos sin bloquear
            var past = objc_msgSend_ret(nsDate, sel("distantPast"));
            while (true)
            {
                var ev = objc_msgSend_ret_event(app,
                    sel("nextEventMatchingMask:untilDate:inMode:dequeue:"),
                    ulong.MaxValue, past, MakeNSString("kCFRunLoopDefaultMode"), true);
                if (ev == IntPtr.Zero) break;

                // NSApplicationDidChangeScreenParametersNotification u otros eventos
                // que invaliden la ventana: marcar para redibujado estático
                needsStaticRedraw = true;
                objc_msgSend_ptr(app, sel("sendEvent:"), ev);
            }

            // Avanzar animación
            var now  = DateTime.UtcNow;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick = now;

            _barPos += _barDir * BAR_SPEED * dt;
            if (_barPos + BAR_BLOCK >= 1f) { _barPos = 1f - BAR_BLOCK; _barDir = -1f; }
            if (_barPos <= 0f)             { _barPos = 0f;              _barDir =  1f; }

            // Redibujar estático solo si hubo eventos que lo invaliden
            if (needsStaticRedraw)
            {
                DrawStatic(contentView);
                needsStaticRedraw = false;
            }

            // Dibujar solo la barra (rápido, sin redibujar imagen ni texto)
            DrawProgress(contentView);

            Thread.Sleep(16);
        }

        // ── Cleanup ───────────────────────────────────────────────────────────
        objc_msgSend_ptr(window, sel("close"), IntPtr.Zero);
        if (_nsImage != IntPtr.Zero)
            objc_msgSend_ptr(_nsImage, sel("release"), IntPtr.Zero);
    }

    // ── Contenido estático: fondo + imagen + texto ────────────────────────────
    private static void DrawStatic(IntPtr view)
    {
        objc_msgSend_ptr(view, sel("lockFocus"), IntPtr.Zero);

        var nsColor = objc_getClass("NSColor");
        var nsBP    = objc_getClass("NSBezierPath");

        // Fondo #1c1c1c
        var bg = objc_msgSend_ret_4d(nsColor, sel("colorWithCalibratedRed:green:blue:alpha:"),
            0.110, 0.110, 0.110, 1.0);
        objc_msgSend_ptr(bg, sel("setFill"), IntPtr.Zero);
        objc_msgSend_rect(nsBP, sel("fillRect:"),
            new NSRect { x = 0, y = 0, width = WIN_W, height = WIN_H });

        // Borde DimGray
        var border = objc_msgSend_ret_4d(nsColor, sel("colorWithCalibratedRed:green:blue:alpha:"),
            0.412, 0.412, 0.412, 0.7);
        objc_msgSend_ptr(border, sel("setStroke"), IntPtr.Zero);
        objc_msgSend_rect(nsBP, sel("strokeRect:"),
            new NSRect { x = 0.5, y = 0.5, width = WIN_W - 1, height = WIN_H - 1 });

        // Imagen centrada (NSImage maneja alpha nativamente)
        if (_nsImage != IntPtr.Zero && _imgW > 0)
        {
            int reservedBottom = 38;
            int availH = WIN_H - reservedBottom - 20;
            int availW = WIN_W - 40;

            int drawW = _imgW, drawH = _imgH;
            if (drawW > availW || drawH > availH)
            {
                float scale = Math.Min((float)availW / drawW, (float)availH / drawH);
                drawW = (int)(drawW * scale);
                drawH = (int)(drawH * scale);
            }

            // macOS: Y=0 es abajo
            double imgX = (WIN_W - drawW) / 2.0;
            double imgY = reservedBottom + (availH - drawH) / 2.0;

            var dst = new NSRect { x = imgX, y = imgY, width = drawW, height = drawH };
            var src = new NSRect { x = 0,    y = 0,    width = _imgW,  height = _imgH };
            objc_msgSend_drawImage(_nsImage,
                sel("drawInRect:fromRect:operation:fraction:"),
                dst, src, 2L /*NSCompositingOperationSourceOver*/, 1.0);
        }

        // Texto "Iniciando..."
        var nsStr  = MakeNSString("Iniciando...");
        double textX = (WIN_W - 70.0) / 2.0;
        double textY = PROGRESS_H + 5.0;
        objc_msgSend_drawStr(nsStr,
            sel("drawAtPoint:withAttributes:"),
            new NSPoint { x = textX, y = textY },
            IntPtr.Zero);

        FlushView(view);
    }

    // ── Contenido dinámico: solo la barra de progreso ─────────────────────────
    private static void DrawProgress(IntPtr view)
    {
        objc_msgSend_ptr(view, sel("lockFocus"), IntPtr.Zero);

        var nsColor   = objc_getClass("NSColor");
        var nsBP      = objc_getClass("NSBezierPath");
        int barY      = 0; // borde inferior en Cocoa
        int barTotalW = WIN_W;
        int blockW    = (int)(barTotalW * BAR_BLOCK);
        int blockX    = (int)(barTotalW * _barPos);

        // Limpiar área de la barra con el fondo
        var bgColor = objc_msgSend_ret_4d(nsColor, sel("colorWithCalibratedRed:green:blue:alpha:"),
            0.110, 0.110, 0.110, 1.0);
        objc_msgSend_ptr(bgColor, sel("setFill"), IntPtr.Zero);
        objc_msgSend_rect(nsBP, sel("fillRect:"),
            new NSRect { x = 0, y = barY, width = WIN_W, height = PROGRESS_H });

        // Bloque animado
        var barColor = objc_msgSend_ret_4d(nsColor, sel("colorWithCalibratedRed:green:blue:alpha:"),
            0.400, 0.733, 1.000, 0.86);
        objc_msgSend_ptr(barColor, sel("setFill"), IntPtr.Zero);
        objc_msgSend_rect(nsBP, sel("fillRect:"),
            new NSRect { x = blockX, y = barY, width = blockW, height = PROGRESS_H });

        FlushView(view);
    }

    private static void FlushView(IntPtr view)
    {
        objc_msgSend_ptr(view, sel("unlockFocus"), IntPtr.Zero);
        var ctx = objc_msgSend_ret(objc_getClass("NSGraphicsContext"), sel("currentContext"));
        if (ctx != IntPtr.Zero)
            objc_msgSend_ptr(ctx, sel("flushGraphics"), IntPtr.Zero);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static IntPtr sel(string name) => sel_registerName(name);

    private static IntPtr MakeNSString(string s)
    {
        var alloc = objc_msgSend_ret(objc_getClass("NSString"), sel("alloc"));
        return objc_msgSend_ret_str(alloc, sel("initWithUTF8String:"), s);
    }

    // ── Structs ───────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)] private struct NSRect  { public double x, y, width, height; }
    [StructLayout(LayoutKind.Sequential)] private struct NSSize  { public double width, height; }
    [StructLayout(LayoutKind.Sequential)] private struct NSPoint { public double x, y; }

    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret(IntPtr r, IntPtr s);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret_ptr(IntPtr r, IntPtr s, IntPtr a);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret_str(IntPtr r, IntPtr s, string a);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret_4d(IntPtr r, IntPtr s, double a, double b, double c, double d);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_ptr(IntPtr r, IntPtr s, IntPtr a = default);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr r, IntPtr s, bool a);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_long(IntPtr r, IntPtr s, long a);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern NSSize objc_msgSend_size(IntPtr r, IntPtr s);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_initWindow(IntPtr r, IntPtr s, NSRect rect, uint a, uint b, bool c);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_rect(IntPtr r, IntPtr s, NSRect rect);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_drawImage(IntPtr r, IntPtr s, NSRect dst, NSRect src, long op, double frac);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_drawStr(IntPtr r, IntPtr s, NSPoint pt, IntPtr attrs);
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret_event(IntPtr r, IntPtr s, ulong mask, IntPtr date, IntPtr mode, bool dequeue);
}
