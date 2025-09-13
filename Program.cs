using Avalonia;
using System;
using System.IO;
using System.Text;
using VManager.Services;

namespace VManager;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Configurar log de consola a archivo
        var logFile = File.CreateText("log.txt");
        logFile.AutoFlush = true;

        // Redirigir Console.WriteLine y Console.Error
        Console.SetOut(new MultiTextWriter(Console.Out, logFile));
        Console.SetError(new MultiTextWriter(Console.Error, logFile));

        // Ejemplo de log
        Console.WriteLine("[DEBUG]: Iniciando VManager...");
        
        FFmpegManager.Initialize();

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