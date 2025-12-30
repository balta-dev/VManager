using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using VManager.Services;
using VManager.Views;

namespace VManager;

sealed class Program
{
    private static readonly string LogsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VManager", "logs");
    
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VManager",
        "config.json");
    
    private sealed class LogConfig
    {
        public bool Log { get; set; } //dto para log
    }
    
    private static bool IsLoggingEnabled()
    {
        if (!File.Exists(ConfigPath))
            return true; // default seguro

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<LogConfig>(json);
            return cfg?.Log ?? true;
        }
        catch
        {
            return true;
        }
    }
    
    [STAThread]
    public static void Main(string[] args)
    {
        var sw = Stopwatch.StartNew();
        MainWindow.StartupStopwatch = sw;
        
        Directory.CreateDirectory(LogsFolder);
        
        if (IsLoggingEnabled())
        {
            var logFilePath = Path.Combine(
                LogsFolder,
                $"log-{DateTime.UtcNow:yyyy-MM-dd}.log"
            );

            var logFile = new StreamWriter(logFilePath, append: true, Encoding.UTF8)
            {
                AutoFlush = true
            };

            Console.SetOut(new MultiTextWriter(Console.Out, logFile));
            Console.SetError(new MultiTextWriter(Console.Error, logFile));
            Console.WriteLine("[DEBUG]: LOG HABILITADO.");

        }
        
        Console.WriteLine("[DEBUG]: Iniciando VManager...");
        
        FFmpegManager.Initialize();
        YtDlpManager.Initialize();

        // Arrancar Avalonia
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // TextWriter que escribe en consola y archivo
    private class MultiTextWriter : TextWriter
    {
        private readonly TextWriter console;
        private readonly TextWriter file;

        public MultiTextWriter(TextWriter console, TextWriter file)
        {
            this.console = console;
            this.file = file;
        }

        public override Encoding Encoding => console.Encoding;

        public override void Write(char value)
        {
            console.Write(value);
            file.Write(value);
        }

        public override void WriteLine(string? value)
        {
            console.WriteLine(value);
            file.WriteLine(value);
        }
    }
}