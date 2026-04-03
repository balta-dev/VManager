using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace VManager.Splash;

/// <summary>
/// Splash screen para Windows usando Win32 + GDI+ puro.
/// Sin dependencias externas.
/// Double buffering para evitar titilado.
/// </summary>
internal static class Win32Splash
{
    private const int WIN_W = 900;
    private const int WIN_H = 220;
    private const int CORNER_RADIUS = 12;
    private const int PROGRESS_H   = 3;

    private static float _barPos  = 0f;
    private static float _barDir  = 1f;
    private const  float BAR_BLOCK = 0.25f;
    private const  float BAR_SPEED = 0.55f;

    private static IntPtr _hwnd;
    private static IntPtr _gdipToken;
    private static IntPtr _image;
    private static int    _imgW, _imgH;

    // Backbuffer: el contenido estático se renderiza aquí una sola vez
    private static IntPtr _backDC        = IntPtr.Zero;
    private static IntPtr _backBmp       = IntPtr.Zero;
    private static bool   _backBufferDirty = true;

    public static void Run(string imagePath, ref bool shouldClose, ref bool isReady)
    {
        // ── GDI+ init ─────────────────────────────────────────────────────────
        var input = new GdiplusStartupInput { GdiplusVersion = 1 };
        GdiplusStartup(out _gdipToken, ref input, out _);

        // ── Cargar imagen (GDI+ maneja alpha nativamente) ─────────────────────
        if (File.Exists(imagePath))
        {
            GdipLoadImageFromFile(imagePath, out _image);
            if (_image != IntPtr.Zero)
            {
                GdipGetImageWidth(_image,  out var w);
                GdipGetImageHeight(_image, out var h);
                _imgW = (int)w;
                _imgH = (int)h;
            }
        }

        // ── Registrar clase de ventana ────────────────────────────────────────
        var className = "VManagerNativeSplash";
        var wndProc   = new WndProcDelegate(WndProc);
        var gcHandle  = GCHandle.Alloc(wndProc);

        var wc = new WNDCLASSEX
        {
            cbSize        = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc   = Marshal.GetFunctionPointerForDelegate(wndProc),
            lpszClassName = className,
            hbrBackground = CreateSolidBrush(0x001C1C1C),
            style         = 0x0003, // CS_HREDRAW | CS_VREDRAW
        };
        RegisterClassEx(ref wc);

        // ── Crear ventana sin bordes, centrada ────────────────────────────────
        int screenW = GetSystemMetrics(0);
        int screenH = GetSystemMetrics(1);
        int x = (screenW - WIN_W) / 2;
        int y = (screenH - WIN_H) / 2;

        _hwnd = CreateWindowEx(
            0x00000008,              // WS_EX_TOPMOST
            className, "VManager",
            0x80000000 | 0x10000000, // WS_POPUP | WS_VISIBLE
            x, y, WIN_W, WIN_H,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        ShowWindow(_hwnd, 5);
        UpdateWindow(_hwnd);

        // ── Crear backbuffer compatible con el DC de la ventana ───────────────
        var screenDC = GetDC(_hwnd);
        _backDC  = CreateCompatibleDC(screenDC);
        _backBmp = CreateCompatibleBitmap(screenDC, WIN_W, WIN_H);
        SelectObject(_backDC, _backBmp);
        ReleaseDC(_hwnd, screenDC);

        isReady = true;

        var lastTick = DateTime.UtcNow;

        while (!shouldClose)
        {
            // Procesar mensajes (WM_PAINT marcará _backBufferDirty)
            while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == 0x0012) goto done; // WM_QUIT
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            // Avanzar animación
            var now  = DateTime.UtcNow;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick = now;

            _barPos += _barDir * BAR_SPEED * dt;
            if (_barPos + BAR_BLOCK >= 1f) { _barPos = 1f - BAR_BLOCK; _barDir = -1f; }
            if (_barPos <= 0f)             { _barPos = 0f;              _barDir =  1f; }

            // Redibujar contenido estático al backbuffer solo si fue invalidado
            if (_backBufferDirty)
            {
                PaintStatic(_backDC);
                _backBufferDirty = false;
            }

            // Dibujar solo la barra encima del backbuffer (rápido, sin redibujar imagen)
            PaintProgress(_backDC);

            // Volcar backbuffer a pantalla de una sola pasada → sin titilado
            var dc = GetDC(_hwnd);
            BitBlt(dc, 0, 0, WIN_W, WIN_H, _backDC, 0, 0, 0x00CC0020 /*SRCCOPY*/);
            ReleaseDC(_hwnd, dc);

            Thread.Sleep(16);
        }

        done:
        DeleteObject(_backBmp);
        DeleteDC(_backDC);
        if (_image != IntPtr.Zero) GdipDisposeImage(_image);
        GdiplusShutdown(_gdipToken);
        if (_hwnd != IntPtr.Zero) DestroyWindow(_hwnd);
        gcHandle.Free();
    }

    // ── WndProc ───────────────────────────────────────────────────────────────
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == 0x000F) // WM_PAINT
        {
            // Marcar el backbuffer como sucio para que se redibuje en el próximo frame
            _backBufferDirty = true;
            // Validar la región para no recibir WM_PAINT infinitos
            BeginPaint(hWnd, out var ps);
            EndPaint(hWnd, ref ps);
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ── Contenido estático: fondo + imagen + texto ────────────────────────────
    // Se dibuja al backbuffer una sola vez (o cuando WM_PAINT lo requiere)
    private static void PaintStatic(IntPtr hdc)
    {
        GdipCreateFromHDC(hdc, out var g);
        GdipSetSmoothingMode(g, 4);     // AntiAlias
        GdipSetInterpolationMode(g, 7); // HighQualityBicubic

        // Fondo redondeado #1c1c1c
        GdipCreateSolidFill(ArgbFromColor(0x1C, 0x1C, 0x1C, 255), out var bgBrush);
        DrawRoundedRect(g, bgBrush, 0, 0, WIN_W, WIN_H, CORNER_RADIUS);
        GdipDeleteBrush(bgBrush);

        // Borde DimGray
        GdipCreatePen1(ArgbFromColor(0x69, 0x69, 0x69, 180), 1f, 2, out var pen);
        GdipDrawRectangleI(g, pen, 0, 0, WIN_W - 1, WIN_H - 1);
        GdipDeletePen(pen);

        // Imagen con alpha (GDI+ lo maneja nativamente, sin blending manual)
        if (_image != IntPtr.Zero)
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

            int imgX = (WIN_W - drawW) / 2;
            int imgY = 20 + (availH - drawH) / 2;
            GdipDrawImageRectI(g, _image, imgX, imgY, drawW, drawH);
        }

        GdipDeleteGraphics(g);

        // Texto "Iniciando..." con GDI directo
        SetBkMode(hdc, 1); // TRANSPARENT
        SetTextColor(hdc, 0x00999999);
        var font    = CreateFont(13, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 0, 0, "Segoe UI");
        var oldFont = SelectObject(hdc, font);
        var text    = "Iniciando...";
        int textX   = (WIN_W - text.Length * 6) / 2;
        TextOut(hdc, textX, WIN_H - 32, text, text.Length);
        SelectObject(hdc, oldFont);
        DeleteObject(font);
    }

    // ── Contenido dinámico: solo la barra de progreso ─────────────────────────
    // Se dibuja encima del backbuffer en cada frame
    private static void PaintProgress(IntPtr hdc)
    {
        int barY      = WIN_H - PROGRESS_H;
        int barTotalW = WIN_W;
        int blockW    = (int)(barTotalW * BAR_BLOCK);
        int blockX    = (int)(barTotalW * _barPos);

        GdipCreateFromHDC(hdc, out var g);

        // Limpiar área de la barra con el color de fondo
        GdipCreateSolidFill(ArgbFromColor(0x1C, 0x1C, 0x1C, 255), out var bgBrush);
        GdipFillRectangle(g, bgBrush, 1, barY, WIN_W - 2, PROGRESS_H);
        GdipDeleteBrush(bgBrush);

        // Bloque animado
        GdipCreateSolidFill(ArgbFromColor(0x66, 0xBB, 0xFF, 220), out var barBrush);
        GdipFillRectangle(g, barBrush, blockX, barY, blockW, PROGRESS_H);
        GdipDeleteBrush(barBrush);

        GdipDeleteGraphics(g);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static uint ArgbFromColor(byte r, byte g, byte b, byte a)
        => (uint)((a << 24) | (r << 16) | (g << 8) | b);

    private static void DrawRoundedRect(IntPtr g, IntPtr brush, int x, int y, int w, int h, int radius)
    {
        GdipCreatePath(0, out var path);
        int d = radius * 2;
        GdipAddPathArcI(path, x,         y,         d, d, 180, 90);
        GdipAddPathArcI(path, x + w - d, y,         d, d, 270, 90);
        GdipAddPathArcI(path, x + w - d, y + h - d, d, d, 0,   90);
        GdipAddPathArcI(path, x,         y + h - d, d, d, 90,  90);
        GdipClosePathFigure(path);
        GdipFillPath(g, brush, path);
        GdipDeletePath(path);
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX, ptY; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc; public bool fErase;
        public int l, t, r, b;
        public bool fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GdiplusStartupInput { public uint GdiplusVersion; public IntPtr DebugCb; public bool SuppressBg; public bool SuppressExternal; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint exStyle, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] static extern bool   ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern bool   UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool   DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] static extern bool   EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool   PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] static extern bool   TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern int    GetSystemMetrics(int n);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int    ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")]  static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")]  static extern bool   BitBlt(IntPtr dst, int xDst, int yDst, int w, int h, IntPtr src, int xSrc, int ySrc, uint rop);
    [DllImport("gdi32.dll")]  static extern bool   DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")]  static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")]  static extern bool   DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]  static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateFont(int h, int w, int esc, int orient, int weight, uint italic, uint under, uint strike, uint charset, uint outPrec, uint clipPrec, uint quality, uint pitchFamily, string face);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern bool   TextOut(IntPtr hdc, int x, int y, string text, int len);
    [DllImport("gdi32.dll")]  static extern int    SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")]  static extern uint   SetTextColor(IntPtr hdc, uint color);

    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)] static extern int  GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, out GdiplusStartupInput output);
    [DllImport("gdiplus.dll")] static extern void GdiplusShutdown(IntPtr token);
    [DllImport("gdiplus.dll", CharSet = CharSet.Unicode)] static extern int  GdipLoadImageFromFile(string file, out IntPtr image);
    [DllImport("gdiplus.dll")] static extern int  GdipDisposeImage(IntPtr image);
    [DllImport("gdiplus.dll")] static extern int  GdipGetImageWidth(IntPtr image, out uint w);
    [DllImport("gdiplus.dll")] static extern int  GdipGetImageHeight(IntPtr image, out uint h);
    [DllImport("gdiplus.dll")] static extern int  GdipCreateFromHDC(IntPtr hdc, out IntPtr g);
    [DllImport("gdiplus.dll")] static extern int  GdipDeleteGraphics(IntPtr g);
    [DllImport("gdiplus.dll")] static extern int  GdipSetSmoothingMode(IntPtr g, int mode);
    [DllImport("gdiplus.dll")] static extern int  GdipSetInterpolationMode(IntPtr g, int mode);
    [DllImport("gdiplus.dll")] static extern int  GdipDrawImageRectI(IntPtr g, IntPtr img, int x, int y, int w, int h);
    [DllImport("gdiplus.dll")] static extern int  GdipCreateSolidFill(uint argb, out IntPtr brush);
    [DllImport("gdiplus.dll")] static extern int  GdipDeleteBrush(IntPtr brush);
    [DllImport("gdiplus.dll")] static extern int  GdipFillRectangle(IntPtr g, IntPtr brush, float x, float y, float w, float h);
    [DllImport("gdiplus.dll")] static extern int  GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);
    [DllImport("gdiplus.dll")] static extern int  GdipDeletePen(IntPtr pen);
    [DllImport("gdiplus.dll")] static extern int  GdipDrawRectangleI(IntPtr g, IntPtr pen, int x, int y, int w, int h);
    [DllImport("gdiplus.dll")] static extern int  GdipCreatePath(int fillMode, out IntPtr path);
    [DllImport("gdiplus.dll")] static extern int  GdipDeletePath(IntPtr path);
    [DllImport("gdiplus.dll")] static extern int  GdipAddPathArcI(IntPtr path, int x, int y, int w, int h, float startAngle, float sweepAngle);
    [DllImport("gdiplus.dll")] static extern int  GdipClosePathFigure(IntPtr path);
    [DllImport("gdiplus.dll")] static extern int  GdipFillPath(IntPtr g, IntPtr brush, IntPtr path);
}
