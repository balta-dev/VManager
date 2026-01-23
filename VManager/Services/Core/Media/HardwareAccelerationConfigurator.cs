using System;
using System.Runtime.InteropServices;
using FFMpegCore;

namespace VManager.Services.Core.Media
{
    public static class HardwareAccelerationConfigurator
    {
        public static void Configure(FFMpegArgumentOptions opts, string videoCodec)
        {
            var codec = videoCodec.ToLower();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConfigureWindows(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ConfigureLinux(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ConfigureMac(opts, codec);
            }
        }

        private static void ConfigureWindows(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    // AV1 NVENC usa perfiles distintos, mejor dejar que ffmpeg decida o ser específico
                    if (codec != "av1_nvenc") opts.WithCustomArgument("-profile:v main");
                    break;

                case "h264_amf":
                case "hevc_amf":
                case "av1_amf":
                    opts.WithCustomArgument("-usage transcoding")
                        .WithCustomArgument("-quality speed")
                        .WithCustomArgument("-rc vbr_peak");
                    if (codec != "av1_amf") opts.WithCustomArgument("-profile:v main");
                    break;

                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-look_ahead 0");
                    if (codec != "av1_qsv") opts.WithCustomArgument("-profile:v main");
                    break;

                case "libx264":
                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
                    
                case "libsvtav1": // Añadimos soporte para el encoder de software AV1
                    opts.WithCustomArgument("-preset 8"); // Preset balanceado para SVT-AV1
                    break;
            }
        }

        private static void ConfigureLinux(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                case "h264_vaapi":
                case "hevc_vaapi":
                case "av1_vaapi":
                case "vp8_vaapi":
                case "vp9_vaapi":
                    opts.WithCustomArgument("-vaapi_device /dev/dri/renderD128")
                        .WithCustomArgument("-vf format=nv12,hwupload");
                    break;

                // AGREGADO: Soporte QSV en Linux (Intel Arc/iGPU)
                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-init_hw_device vaapi=va:/dev/dri/renderD128")
                        .WithCustomArgument("-init_hw_device qsv=hw@va")
                        .WithCustomArgument("-preset fast");
                    break;

                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                case "libx264":
                case "libx265":
                    opts.WithCustomArgument("-preset fast");
                    break;
                    
                case "libsvtav1":
                    opts.WithCustomArgument("-preset 8");
                    break;
            }
        }

        private static void ConfigureMac(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                case "av1_videotoolbox": // <--- AGREGADO
                    opts.WithCustomArgument("-pix_fmt yuv420p") // Vital para compatibilidad en Mac
                        .WithCustomArgument("-realtime 1");    // Optimiza para velocidad
                    
                    if (codec != "av1_videotoolbox") 
                        opts.WithCustomArgument("-profile:v main");
                    break;

                case "prores_videotoolbox":
                    opts.WithCustomArgument("-profile:v 2");
                    break;

                case "libx264":
                case "libx265":
                    opts.WithCustomArgument("-preset fast");
                    break;
            }
        }
        
        public static bool IsDNxHRCodec(string codec)
        {
            return codec.Contains("dnxhd", StringComparison.OrdinalIgnoreCase) || 
                   codec.Contains("dnxhr", StringComparison.OrdinalIgnoreCase);
        }

        public static void ConfigureDNxHROptions(FFMpegArgumentOptions opts, string videoCodec)
        {
            if (IsDNxHRCodec(videoCodec))
            {
                opts.WithCustomArgument("-profile:v dnxhr_hq")
                    .WithCustomArgument("-pix_fmt yuv422p");
            }
        }
    }
}