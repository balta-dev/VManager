using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using VManager.Services;
using Xunit;
using Xunit.Abstractions;

namespace VManager.Tests.Integration
{
    public class FFmpegManagerIntegrationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public FFmpegManagerIntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public async Task InitializeAsync()
        {
            // Inicializar FFmpegManager de forma asíncrona
            await FFmpegManager.Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
        
        [Fact]
        public void FFmpegBinaries_ShouldExistAndBeExecutable()
        {
            // Usar las rutas que FFmpegManager ya determinó
            var ffmpegPath = FFmpegManager.FfmpegPath;
            var ffprobePath = FFmpegManager.FfprobePath;
    
            // Debug: ver qué rutas se asignaron
            _testOutputHelper.WriteLine($"FFmpeg path: {ffmpegPath}");
            _testOutputHelper.WriteLine($"FFprobe path: {ffprobePath}");
    
            // Rutas no vacías
            ffmpegPath.Should().NotBeNullOrEmpty();
            ffprobePath.Should().NotBeNullOrEmpty();
    
            // Archivos existen
            File.Exists(ffmpegPath).Should().BeTrue($"FFmpeg should exist at {ffmpegPath}");
            File.Exists(ffprobePath).Should().BeTrue($"FFprobe should exist at {ffprobePath}");
    
            // Ejecutar para verificar que funcionan
            int exitCodeFfmpeg = RunProcess(ffmpegPath, "-version");
            exitCodeFfmpeg.Should().Be(0, "FFmpeg should execute successfully");
    
            int exitCodeFfprobe = RunProcess(ffprobePath, "-version");
            exitCodeFfprobe.Should().Be(0, "FFprobe should execute successfully");
        }

        private static int RunProcess(string fileName, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
            return process.ExitCode;
        }
    }
}