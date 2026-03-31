using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VManager.Controls;

public partial class ResizeGrip : UserControl
{
    private Window? _window;

    public ResizeGrip()
    {
        InitializeComponent();
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = this.FindAncestorOfType<Window>();
    }

    private void ResizeN(object? sender, PointerPressedEventArgs e)  => _window?.BeginResizeDrag(WindowEdge.North, e);
    private void ResizeS(object? sender, PointerPressedEventArgs e)  => _window?.BeginResizeDrag(WindowEdge.South, e);
    private void ResizeE(object? sender, PointerPressedEventArgs e)  => _window?.BeginResizeDrag(WindowEdge.East, e);
    private void ResizeW(object? sender, PointerPressedEventArgs e)  => _window?.BeginResizeDrag(WindowEdge.West, e);
    private void ResizeNE(object? sender, PointerPressedEventArgs e) => _window?.BeginResizeDrag(WindowEdge.NorthEast, e);
    private void ResizeNW(object? sender, PointerPressedEventArgs e) => _window?.BeginResizeDrag(WindowEdge.NorthWest, e);
    private void ResizeSE(object? sender, PointerPressedEventArgs e) => _window?.BeginResizeDrag(WindowEdge.SouthEast, e);
    private void ResizeSW(object? sender, PointerPressedEventArgs e) => _window?.BeginResizeDrag(WindowEdge.SouthWest, e);
}