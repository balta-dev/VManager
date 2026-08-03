// VManager/Services/StartupOptions.cs

using System;

namespace VManager.Services;

public static class StartupOptions
{
    public static string? RequestedTool { get; private set; }

    public static void Parse(string[] args)
    {
        // Soporta: --tool=vcut  o  --tool vcut
        foreach (var arg in args)
        {
            if (arg.StartsWith("--tool=", StringComparison.OrdinalIgnoreCase))
            {
                RequestedTool = arg["--tool=".Length..].Trim().ToLowerInvariant();
                return;
            }
        }

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--tool", StringComparison.OrdinalIgnoreCase))
            {
                RequestedTool = args[i + 1].Trim().ToLowerInvariant();
                return;
            }
        }
    }
}