using System.IO.Compression;

namespace LotroArchiver;

public static class Archiver
{
    private const string ExcludedRootDirectory = "CEF";

    public static Result Backup(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(config.ProfilePath))
                return Result.Failure($"Profile path not found: {config.ProfilePath}");

            if (!Directory.Exists(config.BackupDirectory))
                Directory.CreateDirectory(config.BackupDirectory);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            var zipName = $"{timestamp}.zip";
            var zipPath = Path.Combine(config.BackupDirectory, zipName);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var rootProfileDir = new DirectoryInfo(config.ProfilePath);

            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // 1. Add top-level files (e.g. lotro.keymap, UserPreferences.ini)
                foreach (var file in rootProfileDir.GetFiles())
                {
                    archive.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
                }

                // 2. Add top-level directories, strictly bypassing the root CEF folder
                foreach (var subDir in rootProfileDir.GetDirectories())
                {
                    if (string.Equals(subDir.Name, ExcludedRootDirectory, StringComparison.OrdinalIgnoreCase))
                        continue;

                    AddDirectoryContents(archive, subDir, rootProfileDir.FullName);
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static void AddDirectoryContents(ZipArchive archive, DirectoryInfo dir, string rootPath)
    {
        foreach (var file in dir.GetFiles())
        {
            var relativePath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/');
            archive.CreateEntryFromFile(file.FullName, relativePath, CompressionLevel.Optimal);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            AddDirectoryContents(archive, subDir, rootPath);
        }
    }

    public static Result Restore(AppConfig config, string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
                return Result.Failure($"Backup file not found: {zipPath}");

            var rootPath = Path.GetFullPath(config.ProfilePath);
            var rootPathWithSeparator = Path.TrimEndingDirectorySeparator(rootPath) + Path.DirectorySeparatorChar;

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(rootPath, entry.FullName));
                
                if (!destinationPath.StartsWith(rootPathWithSeparator, comparison))
                    return Result.Failure($"Malicious zip entry detected: {entry.FullName}");

                var directoryPath = Path.GetDirectoryName(destinationPath);
                
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);
                
                if (!string.IsNullOrEmpty(entry.Name)) 
                    entry.ExtractToFile(destinationPath, overwrite: true);
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}