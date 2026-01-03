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
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                case "h264_amf":
                case "hevc_amf":
                case "av1_amf":
                    opts.WithCustomArgument("-usage transcoding")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-quality speed")
                        .WithCustomArgument("-rc vbr_peak");
                    break;

                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-look_ahead 0");
                    break;

                case "h264_mf":
                case "hevc_mf":
                    break;

                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }

        private static void ConfigureLinux(FFMpegArgumentOptions opts, string codec)
        {
            {
                switch (codec)
                {
                    case "h264_vaapi":
                    case "hevc_vaapi":
                    case "vp8_vaapi":
                    case "vp9_vaapi":
                    case "av1_vaapi":
                        opts.WithCustomArgument("-vaapi_device /dev/dri/renderD128")
                            .WithCustomArgument("-vf format=nv12,hwupload");
                        if (codec == "h264_vaapi" || codec == "hevc_vaapi")
                            opts.WithCustomArgument("-profile:v main");
                        break;

                    case "h264_nvenc":
                    case "hevc_nvenc":
                    case "av1_nvenc":
                        opts.WithCustomArgument("-preset fast")
                            .WithCustomArgument("-profile:v main")
                            .WithCustomArgument("-rc vbr")
                            .WithCustomArgument("-gpu 0");
                        break;

                    case "h264_v4l2m2m":
                    case "vp8_v4l2m2m":
                        opts.WithCustomArgument("-pix_fmt yuv420p");
                        break;

                    case "h264_vulkan":
                    case "hevc_vulkan":
                        opts.WithCustomArgument("-vulkan_params device=0");
                        break;

                    case "libx264":
                        opts.WithCustomArgument("-preset fast")
                            .WithCustomArgument("-profile:v main");
                        break;

                    case "libx265":
                        opts.WithCustomArgument("-preset fast")
                            .WithCustomArgument("-profile:v main");
                        break;
                }
            }
        }

        private static void ConfigureMac(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                    opts.WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-pix_fmt yuv420p")
                        .WithCustomArgument("-realtime 0");
                    break;

                case "prores_videotoolbox":
                    opts.WithCustomArgument("-profile:v 2");
                    break;

                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
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