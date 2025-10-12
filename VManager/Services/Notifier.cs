using System;
using System.Diagnostics;
using System.IO;
using VManager.Views;
using VManager.ViewModels;

namespace VManager.Services;
public class Notifier
{
    private static bool _enabled = true;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                System.Console.WriteLine($"Notificaciones {(value ? "activadas" : "desactivadas")}");
            }
        }
    }
    
    public void ShowNotificationLinux(string title, string message)
    {
        var iconPath = Path.GetFullPath("Assets/VManager.ico");
        Process.Start(new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = $"\"{title}\" \"{message}\" -i \"{iconPath}\"",
            UseShellExecute = true
        });
    }
    
    public void ShowNotificationMac(string title, string message)
    {
        string script = $"display notification \"{message}\" with title \"{title}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e \"{script}\"",
            UseShellExecute = true
        });
    }
    
    public void ShowFileConvertedNotification(string status, string filePath)
    {
        if (!Enabled)
            return;
        
        string fileName = Path.GetFileName(filePath);
        
        if (OperatingSystem.IsLinux())
        {
            ShowNotificationLinux(status, $"¡El archivo {fileName} fue procesado exitosamente!");
        }
        else if (OperatingSystem.IsMacOS())
        {
            ShowNotificationMac(status, $"¡El archivo {fileName} fue procesado exitosamente!");
        }
        else if (OperatingSystem.IsWindows())
        {
            var toast = new ToastWindow(
                status,
                $"¡El archivo {fileName} fue procesado exitosamente!",
                "Assets/VManager.ico"
            );
            toast.Show(); 
        }
    }
    
}
