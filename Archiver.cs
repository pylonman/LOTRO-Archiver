using System.IO.Compression;

namespace LotroArchiver;

public static class Archiver
{
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

            ZipFile.CreateFromDirectory(config.ProfilePath, zipPath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public static Result Restore(AppConfig config, string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
                return Result.Failure($"Backup file not found: {zipPath}");

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.Combine(config.ProfilePath, entry.FullName);
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