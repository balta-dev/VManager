// Services/Core/Constants.cs
namespace VManager.Services.Core
{
    internal static class ProcessingConstants
    {
        public const string CutSuffix = "-VCUT";
        public const string CompressSuffixPattern = "-{0}";
        public const string ConvertSuffix = "-VCONV";
        public const string AudioSuffix = "-ACONV";
        public const string TempFolderName = "vmanager_temp";
        public const string ChunkPrefix = "chunk_";
        public const string ProcessedChunkPrefix = "processed_";
        public const string ProgressLogFile = "vmanager_progress.json";
        public const string ConcatListFile = "concat_list.txt";
    }
    
    internal static class ErrorMessages
    {
        public const string FileNotFound = "Archivo no encontrado.";
        public const string InvalidPercentage = "Porcentaje inválido.";
        public const string AnalysisError = "Error al analizar el video: {0}";
        public const string InvalidDuration = "Error al obtener duración.";
        public const string InvalidCutParameters = "Parámetros de corte inválidos.";
        public const string InvalidAudioFormat = "Formato de audio no especificado.";
        public const string NoAudioStream = "El archivo no contiene pistas de audio.";
    }
}