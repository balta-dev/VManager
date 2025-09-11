using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using System.Threading.Tasks;

namespace VManager.Views;

public partial class MainWindow : Window
{
    private string _hoverSoundPath;
    private string _clickSoundPath;
    private string _toggleSoundPath;

    public MainWindow()
    {
        InitializeComponent();
        InitializeSounds();
        AttachSoundEvents();
    }

    private void InitializeSounds()
    {
        try
        {
            string projectPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory))));
            _hoverSoundPath = Path.Combine(projectPath, "Assets", "Sounds", "hover.wav");
            _clickSoundPath = Path.Combine(projectPath, "Assets", "Sounds", "click.wav");
            _toggleSoundPath = Path.Combine(projectPath, "Assets", "Sounds", "toggletheme.wav");

            Console.WriteLine($"Hover: {_hoverSoundPath} (exists: {File.Exists(_hoverSoundPath)})");
            Console.WriteLine($"Click: {_clickSoundPath} (exists: {File.Exists(_clickSoundPath)})");
            Console.WriteLine($"Toggle: {_toggleSoundPath} (exists: {File.Exists(_toggleSoundPath)})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error inicializando sonidos: {ex.Message}");
        }
    }

    private void Button_PointerEnter(object? sender, PointerEventArgs e)
    {
        PlaySoundAsync(_hoverSoundPath);
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        PlaySoundAsync(_clickSoundPath);
    }
    
    private void Button_Toggle(object? sender, RoutedEventArgs e)
    {
        PlaySoundAsync(_toggleSoundPath);
    }

    private void PlaySoundAsync(string soundPath)
    {
        if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
            return;

        Task.Run(() =>
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    // Usar aplay (ALSA) en Linux - no bloquea
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "aplay",
                        Arguments = $"\"{soundPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });
                    
                    // No esperar - fire and forget
                    Console.WriteLine($"Reproduciendo {Path.GetFileName(soundPath)} con aplay");
                }
                else if (OperatingSystem.IsWindows())
                {
                    // En Windows usar PowerShell
                    var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-c \"(New-Object Media.SoundPlayer '{soundPath}').PlaySync()\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reproduciendo sonido: {ex.Message}");
            }
        });
    }

    private void AttachSoundEvents()
    {
        var buttons = this.GetLogicalDescendants().OfType<Button>();
        foreach (var button in buttons)
        {
            if (button.Name == "ToggleTheme")
            {
                button.PointerEntered += Button_PointerEnter;
                button.Click += Button_Toggle;
            }
            else
            {
                button.PointerEntered += Button_PointerEnter;
                button.Click += Button_Click;
            }
        }
        
    }
}