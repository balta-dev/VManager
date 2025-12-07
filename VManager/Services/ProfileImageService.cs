using System;
using System.IO;
using System.Threading.Tasks;

namespace VManager.Services
{
    public static class ProfileImageService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager"
        );
        
        private static readonly string ProfileImagesPath = Path.Combine(AppDataPath, "ProfileImages");
        
        public static string DefaultProfileImagePath => Path.Combine(ProfileImagesPath, "profile.jpg");

        // Firmas mágicas (magic numbers) de formatos de imagen válidos
        private static readonly byte[][] ValidImageSignatures = new[]
        {
            new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, // PNG
            new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, // GIF87a
            new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, // GIF89a
            new byte[] { 0x42, 0x4D }, // BMP
            new byte[] { 0x49, 0x49, 0x2A, 0x00 }, // TIFF (little-endian)
            new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, // TIFF (big-endian)
            new byte[] { 0x52, 0x49, 0x46, 0x46 }, // WebP (RIFF header)
        };

        static ProfileImageService()
        {
            // Crear directorios si no existen
            Directory.CreateDirectory(ProfileImagesPath);
        }

        /// <summary>
        /// Valida que el archivo sea realmente una imagen verificando su firma mágica
        /// </summary>
        public static async Task<(bool IsValid, string Message)> ValidateImageFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return (false, "El archivo no existe");

            try
            {
                // Leer los primeros bytes del archivo
                byte[] headerBytes = new byte[12]; // Suficiente para cubrir las firmas más largas
                
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int bytesRead = await fs.ReadAsync(headerBytes, 0, headerBytes.Length);
                    
                    if (bytesRead < 2)
                        return (false, "El archivo es demasiado pequeño para ser una imagen válida");
                }

                // Verificar si coincide con alguna firma válida
                bool isValidImage = false;
                foreach (var signature in ValidImageSignatures)
                {
                    if (StartsWithSignature(headerBytes, signature))
                    {
                        isValidImage = true;
                        break;
                    }
                }

                if (!isValidImage)
                    return (false, "El archivo no es una imagen válida. Solo se permiten JPG, PNG, GIF, BMP, TIFF y WebP");

                // Verificar tamaño razonable (máximo 10 MB)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024)
                    return (false, "La imagen es demasiado grande. Máximo 10 MB");

                return (true, "Imagen válida");
            }
            catch (Exception ex)
            {
                return (false, $"Error al validar la imagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica si los bytes comienzan con la firma dada
        /// </summary>
        private static bool StartsWithSignature(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                if (data[i] != signature[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Guarda una copia de la imagen en AppData y retorna la ruta
        /// </summary>
        public static async Task<(bool Success, string Path, string Message)> SaveProfileImageAsync(string sourcePath)
        {
            // Validar primero
            var validation = await ValidateImageFileAsync(sourcePath);
            if (!validation.IsValid)
                return (false, "", validation.Message);

            try
            {
                // Obtener extensión del archivo original
                string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                
                // Nombre del archivo destino
                string destinationFileName = $"profile{extension}";
                string destinationPath = Path.Combine(ProfileImagesPath, destinationFileName);

                // Eliminar archivo anterior si existe
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);

                // Copiar el archivo
                await Task.Run(() => File.Copy(sourcePath, destinationPath, true));

                return (true, destinationPath, "Imagen de perfil guardada correctamente");
            }
            catch (Exception ex)
            {
                return (false, "", $"Error al guardar la imagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina la imagen de perfil actual
        /// </summary>
        public static (bool Success, string Message) DeleteProfileImage()
        {
            try
            {
                // Buscar cualquier archivo que empiece con "profile"
                var profileFiles = Directory.GetFiles(ProfileImagesPath, "profile.*");
                
                foreach (var file in profileFiles)
                {
                    File.Delete(file);
                }

                return (true, "Imagen de perfil eliminada");
            }
            catch (Exception ex)
            {
                return (false, $"Error al eliminar la imagen: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene la ruta de la imagen de perfil actual (si existe)
        /// </summary>
        public static string? GetCurrentProfileImagePath()
        {
            try
            {
                var profileFiles = Directory.GetFiles(ProfileImagesPath, "profile.*");
                return profileFiles.Length > 0 ? profileFiles[0] : null;
            }
            catch
            {
                return null;
            }
        }
    }
}