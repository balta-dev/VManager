using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VManager.Services;
using VManager.Services.Models;
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
    
    private static bool IsLoggingEnabled()
    {
        if (!File.Exists(ConfigPath))
            return true; // default seguro

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<LogConfig>(json, VManagerJsonContext.Default.LogConfig);
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
        var t0 = sw.ElapsedMilliseconds;
        
        if (IsLoggingEnabled())
        {
            
            t0 = sw.ElapsedMilliseconds;
            
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
            
            Console.WriteLine($"[STARTUP] [{sw.ElapsedMilliseconds}ms] StreamWriter setup (delta: {sw.ElapsedMilliseconds - t0}ms)");

        }
        
        Console.WriteLine("[DEBUG]: Iniciando VManager...");
        
        // Medimos cuánto tarda en RETORNAR cada Initialize (no en completarse)
        // Si tarda >5ms en retornar, tiene código síncrono bloqueante antes del primer await
        
        var ffmpegTask = FFmpegManager.Initialize();
        Console.WriteLine($"[STARTUP] [{sw.ElapsedMilliseconds}ms] FFmpegManager.Initialize() retornó (delta: {sw.ElapsedMilliseconds - t0}ms)");
    
        t0 = sw.ElapsedMilliseconds;
        var ytDlpTask = YtDlpManager.Initialize();
        Console.WriteLine($"[STARTUP] [{sw.ElapsedMilliseconds}ms] YtDlpManager.Initialize() retornó (delta: {sw.ElapsedMilliseconds - t0}ms)");
    
        t0 = sw.ElapsedMilliseconds;
        var denoTask = DenoManager.Initialize();
        Console.WriteLine($"[STARTUP] [{sw.ElapsedMilliseconds}ms] DenoManager.Initialize() retornó (delta: {sw.ElapsedMilliseconds - t0}ms)");
    
        Console.WriteLine($"[STARTUP] [{sw.ElapsedMilliseconds}ms] Arrancando Avalonia...");

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