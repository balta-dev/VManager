namespace VManager.Services.Models
{
    public class ProcessingResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string OutputPath { get; }
        public string Warning { get; }

        public ProcessingResult(bool success, string message, string outputPath = "", string warning = "")
        {
            Success = success;
            Message = message;
            OutputPath = outputPath;
            Warning = warning;
        }
    }
}