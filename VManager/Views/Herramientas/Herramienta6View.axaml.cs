using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using VManager.Behaviours.X11DragDrop;
using VManager.ViewModels.Herramientas;

namespace VManager.Views.Herramientas;

public partial class Herramienta6View : SoundEnabledUserControl
{
    private X11DragFeedbackApplier? _feedbackApplier;
    private double _valueAtDragStart;
    private PixelPoint _lockedScreenPos;

    // ── Windows ───────────────────────────────────────────────────────────────
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);

    // ── Linux (X11) ───────────────────────────────────────────────────────────
    [DllImport("libX11")] static extern IntPtr XOpenDisplay(string? display);
    [DllImport("libX11")] static extern int XWarpPointer(IntPtr display, IntPtr src,
        IntPtr dest, int srcX, int srcY, uint srcW, uint srcH, int destX, int destY);
    [DllImport("libX11")] static extern int XFlush(IntPtr display);
    [DllImport("libX11")] static extern uint XRootWindow(IntPtr display, int screen);
    [DllImport("libX11")] static extern int XDefaultScreen(IntPtr display);

    // ── macOS ─────────────────────────────────────────────────────────────────
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    private struct CGPoint { public double x, y; }

    private IntPtr _x11Display = IntPtr.Zero;

    private void LockCursorToScreenPos(PixelPoint pos)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetCursorPos(pos.X, pos.Y);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (_x11Display == IntPtr.Zero) return;
            var screen = XDefaultScreen(_x11Display);
            var root = (IntPtr)XRootWindow(_x11Display, screen);
            XWarpPointer(_x11Display, IntPtr.Zero, root, 0, 0, 0, 0, pos.X, pos.Y);
            XFlush(_x11Display);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            CGWarpMouseCursorPosition(new CGPoint { x = pos.X, y = pos.Y });
        }
    }

    private Point _lastPosition;
    private bool _isWarping;
    private void OnSpeedPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var vm = DataContext as Herramienta6ViewModel;
        if (vm == null) return;
        double.TryParse(vm.Speed, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _valueAtDragStart);
        e.Pointer.Capture((IInputElement)sender);
        _lockedScreenPos = this.PointToScreen(e.GetPosition(this));
        _lastPosition = e.GetPosition(this);
        _isWarping = false;
        ((InputElement)sender).Cursor = new Cursor(StandardCursorType.None);
    }

    private void OnSpeedPointerMoved(object sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
    
        if (_isWarping)
        {
            _isWarping = false;
            _lastPosition = e.GetPosition(this); // resetear después del warp
            return;
        }
    
        var vm = DataContext as Herramienta6ViewModel;
        if (vm == null) return;

        var currentPos = e.GetPosition(this);
        var deltaX = currentPos.X - _lastPosition.X;

        if (Math.Abs(deltaX) < 0.5) return;

        var multiplier = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 0.001 : 0.01;
        var newValue = Math.Max(0.1, _valueAtDragStart + deltaX * multiplier);
        vm.Speed = newValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _valueAtDragStart = newValue;

        _isWarping = true;
        LockCursorToScreenPos(_lockedScreenPos);
    }
    
    private void OnSpeedPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        ((InputElement)sender).Cursor = new Cursor(StandardCursorType.SizeWestEast);
        LockCursorToScreenPos(_lockedScreenPos);
    }
    
    public Herramienta6View()
    {
        InitializeComponent();

        if (OperatingSystem.IsLinux())
        {
            _x11Display = XOpenDisplay(null);

            var border = this.FindControl<Border>("DropZoneBorder");
            if (border != null)
                _feedbackApplier = new X11DragFeedbackApplier(border);
        }
    }
}