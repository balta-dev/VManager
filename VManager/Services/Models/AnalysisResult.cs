namespace VManager.Services.Models
{
    public class AnalysisResult<T>
    {
        public bool Success { get; }
        public string Message { get; }
        public T? Result { get; }

        public AnalysisResult(bool success, string message, T? result = default)
        {
            Success = success;
            Message = message;
            Result = result;
        }
    }
}