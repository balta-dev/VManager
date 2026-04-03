using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VManager.Splash;

public static class NativeSplash
{
    private static Thread?       _thread;
    private static volatile bool _shouldClose;
    private static volatile bool _isReady;

    public static void Show(string imagePath)
    {
        _shouldClose = false;
        _isReady     = false;

        ThreadStart body =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? () => Win32Splash.Run(imagePath,  ref _shouldClose, ref _isReady) :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)   ? () => X11Splash.Run(imagePath,    ref _shouldClose, ref _isReady) :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? () => CocoaSplash.Run(imagePath,  ref _shouldClose, ref _isReady) :
                                                                   () => { _isReady = true; };

        _thread = new Thread(body)
        {
            IsBackground = true,
            Name         = "NativeSplashThread"
        };
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        // Esperar hasta que la ventana sea visible (máx 1s)
        var deadline = DateTime.UtcNow.AddMilliseconds(1000);
        while (!_isReady && DateTime.UtcNow < deadline)
            Thread.Sleep(5);
    }

    public static void Close()
    {
        _shouldClose = true;
        _thread?.Join(2000);
        _thread = null;
    }
}
