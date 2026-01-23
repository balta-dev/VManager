using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace VManager.Services.Core
{
    public static class ProfileImageService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager"
        );
        
        private static readonly string ProfileImagesPath = Path.Combine(AppDataPath, "ProfileImages");
        
        public static string DefaultProfileImagePath => Path.Combine(ProfileImagesPath, "profile.jpg");

        private const int ProfileImageSize = 128;

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
                ErrorService.Show(ex);
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
        /// Redimensiona una imagen a 128x128 manteniendo el aspect ratio y centrando
        /// </summary>
        private static async Task<bool> ResizeAndSaveImageAsync(string sourcePath, string destinationPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var inputStream = File.OpenRead(sourcePath))
                    using (var original = SKBitmap.Decode(inputStream))
                    {
                        if (original == null)
                            return false;

                        // Crear bitmap de salida de 128x128
                        using (var resized = new SKBitmap(ProfileImageSize, ProfileImageSize))
                        using (var canvas = new SKCanvas(resized))
                        {
                            // Fondo blanco (o transparente si prefieres)
                            canvas.Clear(SKColors.White);

                            // Calcular el escalado manteniendo el aspect ratio
                            float scale = Math.Min(
                                (float)ProfileImageSize / original.Width,
                                (float)ProfileImageSize / original.Height
                            );

                            int scaledWidth = (int)(original.Width * scale);
                            int scaledHeight = (int)(original.Height * scale);

                            // Centrar la imagen
                            int x = (ProfileImageSize - scaledWidth) / 2;
                            int y = (ProfileImageSize - scaledHeight) / 2;

                            var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);
                            var srcRect = new SKRect(0, 0, original.Width, original.Height);

                            // Dibujar con filtro de alta calidad
                            var paint = new SKPaint
                            {
                                FilterQuality = SKFilterQuality.High,
                                IsAntialias = true
                            };

                            canvas.DrawBitmap(original, srcRect, destRect, paint);

                            // Guardar como PNG para mantener calidad
                            using (var image = SKImage.FromBitmap(resized))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 95))
                            using (var stream = File.OpenWrite(destinationPath))
                            {
                                data.SaveTo(stream);
                            }
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Guarda una copia redimensionada de la imagen en AppData y retorna la ruta
        /// </summary>
        public static async Task<(bool Success, string Path, string Message)> SaveProfileImageAsync(string sourcePath)
        {
            // Validar primero
            var validation = await ValidateImageFileAsync(sourcePath);
            if (!validation.IsValid)
                return (false, "", validation.Message);

            try
            {
                // Siempre guardar como PNG después del resize
                string destinationFileName = "profile.png";
                string destinationPath = Path.Combine(ProfileImagesPath, destinationFileName);

                // Eliminar archivo anterior si existe
                DeleteProfileImage();

                // Redimensionar y guardar
                bool success = await ResizeAndSaveImageAsync(sourcePath, destinationPath);

                if (!success)
                    return (false, "", "Error al procesar la imagen");

                return (true, destinationPath, "Imagen de perfil guardada correctamente (128x128)");
            }
            catch (Exception ex)
            {
                ErrorService.Show(ex);
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
                ErrorService.Show(ex);
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