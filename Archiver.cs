using System.IO.Compression;

namespace LotroArchiver;

public static class Archiver
{
    private const string ExcludedRootDirectory = "CEF";
    private const string PreRestorePrefix = "pre-restore_";

    /// <summary>
    /// Creates a timestamped zip of the profile. The archive is built in a .tmp file and
    /// only moved to its final name once it is complete, so a failed run never destroys
    /// or replaces an existing backup. Returns the path of the new zip.
    /// </summary>
    public static Result<string> Backup(AppConfig config, string namePrefix = "")
    {
        string? tempPath = null;
        try
        {
            if (!Directory.Exists(config.ProfilePath))
                return Result<string>.Failure($"Profile path not found: {config.ProfilePath}");

            Directory.CreateDirectory(config.BackupDirectory); // no-op if it already exists

            var zipPath = Path.Combine(config.BackupDirectory, $"{namePrefix}{DateTime.Now:yyyy-MM-dd}.zip");
            tempPath = zipPath + ".tmp";

            var rootProfileDir = new DirectoryInfo(config.ProfilePath);

            using (var zipStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // 1. Top-level files (e.g. lotro.keymap, UserPreferences.ini)
                foreach (var file in rootProfileDir.GetFiles())
                {
                    archive.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
                }

                // 2. Top-level directories, bypassing the root CEF folder
                foreach (var subDir in rootProfileDir.GetDirectories())
                {
                    if (string.Equals(subDir.Name, ExcludedRootDirectory, StringComparison.OrdinalIgnoreCase))
                        continue;

                    AddDirectoryContents(archive, subDir, rootProfileDir.FullName);
                }
            }

            File.Move(tempPath, zipPath, overwrite: true);
            tempPath = null; // success: nothing to clean up

            return Result<string>.Success(zipPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ex.Message);
        }
        finally
        {
            if (tempPath is not null) TryDelete(tempPath);
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

    /// <summary>
    /// Restores a backup over the profile directory. Order of operations:
    ///   1. Validate every entry (zip-slip check) before touching the disk.
    ///   2. Snapshot the current profile into a "pre-restore_*" zip in the backup directory.
    ///   3. Extract.
    /// If step 1 or 2 fails, the profile is untouched. If step 3 fails partway, the error
    /// message points at the snapshot. On success, Value is the snapshot path (empty string
    /// if the profile was empty or missing and no snapshot was needed).
    /// </summary>
    public static Result<string> Restore(AppConfig config, string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
                return Result<string>.Failure($"Backup file not found: {zipPath}");

            var rootPath = Path.GetFullPath(config.ProfilePath);
            var rootPathWithSeparator = Path.TrimEndingDirectorySeparator(rootPath) + Path.DirectorySeparatorChar;

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            using var archive = ZipFile.OpenRead(zipPath);

            // 1. Validate everything up front
            var plan = new List<(ZipArchiveEntry Entry, string Destination)>(archive.Entries.Count);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(rootPath, entry.FullName));

                if (!destinationPath.StartsWith(rootPathWithSeparator, comparison))
                    return Result<string>.Failure($"Malicious zip entry detected: {entry.FullName}. Profile unchanged.");

                plan.Add((entry, destinationPath));
            }

            // 2. Snapshot the current profile so a bad restore is recoverable
            var safetyBackupPath = string.Empty;
            if (Directory.Exists(rootPath) && Directory.EnumerateFileSystemEntries(rootPath).Any())
            {
                var safety = Backup(config, PreRestorePrefix);
                if (!safety.IsSuccess)
                    return Result<string>.Failure($"Could not create a safety backup of the current profile: {safety.Error}. Restore aborted; profile unchanged.");

                safetyBackupPath = safety.Value!;
            }

            // 3. Extract
            try
            {
                foreach (var (entry, destinationPath) in plan)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // Directory entry
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                var hint = safetyBackupPath.Length > 0
                    ? $" Your previous profile was saved to: {safetyBackupPath}"
                    : string.Empty;
                return Result<string>.Failure($"Restore failed partway through: {ex.Message}.{hint}");
            }

            return Result<string>.Success(safetyBackupPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ex.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* best effort */ }
    }
}