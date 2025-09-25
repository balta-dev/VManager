using System;
using System.Diagnostics;
using System.IO;
using VManager.Views;

namespace VManager.Services;
public class Notifier
{
    public void ShowNotificationLinux(string title, string message)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = $"\"{title}\" \"{message}\"",
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
