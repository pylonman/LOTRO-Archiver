using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace LotroArchiver;

public static class SteamDetector
{
    private const string LotroAppId = "212500";

    public static List<string> FindLotroInstallPaths()
    {
        var paths = new List<string>();
        var steamPath = GetSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return paths;

        var libraryPaths = GetLibraryPaths(steamPath);
        
        foreach (var libPath in libraryPaths)
        {
            // Check for app manifest to confirm game is in this library
            var appManifestPath = Path.Combine(libPath, "steamapps", $"appmanifest_{LotroAppId}.acf");
            if (File.Exists(appManifestPath))
            {
                var installDir = GetInstallDirFromManifest(appManifestPath);
                if (!string.IsNullOrEmpty(installDir))
                {
                    var gamePath = Path.Combine(libPath, "steamapps", "common", installDir);
                    if (Directory.Exists(gamePath)) paths.Add(gamePath);
                }
            }
        }

        return paths;
    }

    public static List<string> FindLotroProfilePaths()
    {
        var paths = new List<string>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return paths;

        var steamPath = GetSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return paths;

        var libraryPaths = GetLibraryPaths(steamPath);
        
        foreach (var libPath in libraryPaths)
        {
            var path = Path.Combine(libPath, "steamapps", "compatdata", LotroAppId, "pfx", "drive_c", "users", "steamuser", "Documents", "The Lord of the Rings Online");
            if (Directory.Exists(path)) paths.Add(path);
        }

        return paths;
    }

    private static string? GetSteamPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetSteamPathWindows();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return GetSteamPathLinux();
            
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return GetSteamPathOsx();
            
        return null;
    }

    private static string? GetSteamPathWindows()
    {
        // Try registry first
        try 
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "query HKCU\\Software\\Valve\\Steam /v SteamPath",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                var match = Regex.Match(output, @"SteamPath\s+REG_SZ\s+(.+)");
                if (match.Success)
                {
                    // Steam often stores path with forward slashes in registry
                    return match.Groups[1].Value.Trim().Replace("/", "\\");
                }
            }
        }
        catch {}

        // Fallback to standard paths
        var progFiles86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        if (!string.IsNullOrEmpty(progFiles86))
        {
            var path = Path.Combine(progFiles86, "Steam");
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    private static string? GetSteamPathLinux()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[] 
        {
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam")
        };
        foreach(var p in paths) if (Directory.Exists(p)) return p;
        return null;
    }

    private static string? GetSteamPathOsx()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, "Library", "Application Support", "Steam");
        if (Directory.Exists(path)) return path;
        return null;
    }

    private static List<string> GetLibraryPaths(string steamPath)
    {
        var paths = new List<string> { steamPath };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        
        if (File.Exists(vdfPath))
        {
            try
            {
                var lines = File.ReadAllLines(vdfPath);
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        var path = match.Groups[1].Value;
                        // Unescape double backslashes for Windows paths
                        path = path.Replace("\\\\", "\\");
                        if (Directory.Exists(path)) paths.Add(path);
                    }
                }
            }
            catch {}
        }
        
        return paths.Distinct().ToList();
    }

    private static string? GetInstallDirFromManifest(string manifestPath)
    {
        try
        {
            var lines = File.ReadAllLines(manifestPath);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, "\"installdir\"\\s+\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
        }
        catch {}
        return null;
    }
}