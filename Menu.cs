using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace LotroArchiver;

public static class Menu
{
    public static Result<AppConfig> RunSetup(string configPath)
    {
        AnsiConsole.MarkupLine("[yellow]Starting setup...[/]");

        // 1. Detect Profile Path (Documents/The Lord of the Rings Online)
        var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        List<string> profilePaths =
        [
            Path.Combine(docPath, "The Lord of the Rings Online"),
            .. SteamDetector.FindLotroProfilePaths()
        ];

        string profilePath = DetectOrAsk("Profile Data", profilePaths);

        // 2. Detect Install Path (Steam or Standalone)

        var installPaths = SteamDetector.FindLotroInstallPaths();
        string installPath = DetectOrAsk("Game Installation", installPaths);

        // 3. Set Backup Path
        var defaultBackup = Path.Combine(docPath, "LotroArchiver", "backup");

        var config = new AppConfig(profilePath, installPath, defaultBackup);

        var saveResult = Configuration.Save(configPath, config);
        if (!saveResult.IsSuccess) return Result<AppConfig>.Failure(saveResult.Error!);

        return Result<AppConfig>.Success(config);
    }

    private static string DetectOrAsk(string name, IEnumerable<string> detectedPaths)
    {
        var validPaths = detectedPaths.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct().ToList();

        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            if (validPaths.Any())
            {
                var path = validPaths.First();
                AnsiConsole.MarkupLine($"[yellow]Non-interactive mode detected. Using detected path for {name}: {path}[/]");
                return path;
            }

            throw new InvalidOperationException($"Cannot prompt for {name} in non-interactive mode. Please configure manually or run in an interactive terminal.");
        }

        foreach (var path in validPaths)
        {
            if (AnsiConsole.Confirm($"Detected {name} at [green]{path}[/]. Use this?"))
            {
                return path;
            }
        }

        AnsiConsole.MarkupLine($"[yellow]Could not automatically detect {name}.[/]");
        return AnsiConsole.Prompt(
            new TextPrompt<string>($"Please enter the full path to your [green]{name}[/]:")
                .Validate(path => Directory.Exists(path)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("[red]Path does not exist[/]")));
    }

    public static void ShowMainMenu(AppConfig config, string configPath)
    {
        bool keepRunning = true;
        while (keepRunning)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("LOTRO Archiver").Color(Color.Green));

            var table = new Table();
            table.AddColumn("Setting");
            table.AddColumn("Path");
            table.AddRow("Profile", config.ProfilePath);
            table.AddRow("Install", config.InstallPath);
            table.AddRow("Backups", config.BackupDirectory);
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<MenuAction>()
                    .Title("What would you like to do?")
                    .AddChoices(Enum.GetValues<MenuAction>())
                    .UseConverter(GetMenuDescription));

            (keepRunning, config) = HandleMenuSelection(choice, config, configPath);
        }
    }

    private static (bool KeepRunning, AppConfig Config) HandleMenuSelection(MenuAction choice, AppConfig config, string configPath)
    {
        switch (choice)
        {
            case MenuAction.BackupProfile:
                if (GameProcess.IsRunning())
                {
                    AnsiConsole.MarkupLine("[red]Error: The game is currently running. Please close it before backing up.[/]");
                    WaitForInput();
                    break;
                }

                Result<string> backupResult = default;
                AnsiConsole.Status().Start("Backing up...", _ =>
                {
                    backupResult = Archiver.Backup(config);
                });

                if (backupResult.IsSuccess)
                {
                    AnsiConsole.MarkupLine("[green]Backup Complete![/]");
                    AnsiConsole.MarkupLine($"Saved to [green]{Markup.Escape(backupResult.Value!)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Backup Failed: {Markup.Escape(backupResult.Error!)}[/]");
                }
                WaitForInput();
                break;
            case MenuAction.RestoreProfile:
                if (GameProcess.IsRunning())
                {
                    AnsiConsole.MarkupLine("[red]Error: The game is currently running. Please close it before restoring.[/]");
                    WaitForInput();
                    break;
                }

                var backupFiles = Directory.Exists(config.BackupDirectory)
                    ? new DirectoryInfo(config.BackupDirectory).GetFiles("*.zip")
                        .OrderByDescending(f => f.LastWriteTimeUtc).ToList()
                        : [];

                if (backupFiles.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No backups found.[/]");
                    WaitForInput();
                    break;
                }

                var selectedName = AnsiConsole.Prompt(
                    new SelectionPrompt<string> { CancelResult = () => CancelChoice }
                        .Title("Select a backup to restore: [grey](Esc to cancel)[/]")
                        .UseConverter(Markup.Escape)
                        .AddChoices(backupFiles.Select(f => f.Name))
                        .AddChoices(CancelChoice));

                if (selectedName == CancelChoice) break;

                var confirmed = AnsiConsole.Confirm(
                    $"This will overwrite files in [green]{Markup.Escape(config.ProfilePath)}[/] with the contents of " +
                    $"[green]{Markup.Escape(selectedName)}[/]. Your current profile will be saved first. Continue?",
                    defaultValue: false);
                if (!confirmed) break;

                Result<string> restoreResult = default;
                AnsiConsole.Status().Start("Restoring...", _ =>
                {
                    restoreResult = Archiver.Restore(config, Path.Combine(config.BackupDirectory, selectedName));
                });

                if (restoreResult.IsSuccess)
                {
                    AnsiConsole.MarkupLine("[green]Restore Complete![/]");
                    if (!string.IsNullOrEmpty(restoreResult.Value))
                        AnsiConsole.MarkupLine($"Previous profile saved to [green]{Markup.Escape(restoreResult.Value)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Restore Failed: {Markup.Escape(restoreResult.Error!)}[/]");
                }
                WaitForInput();
                break;
            case MenuAction.ViewBackups:
                if (!Directory.Exists(config.BackupDirectory))
                {
                    AnsiConsole.MarkupLine("[yellow]No backup directory found.[/]");
                }
                else
                {
                    var files = new DirectoryInfo(config.BackupDirectory).GetFiles("*.zip");
                    if (files.Length == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No backups found.[/]");
                    }
                    else
                    {
                        var backupTable = new Table();
                        backupTable.AddColumn("File Name");
                        backupTable.AddColumn("Created");
                        backupTable.AddColumn("Size");
                        foreach (var file in files.OrderByDescending(f => f.CreationTime))
                        {
                            backupTable.AddRow(file.Name, file.CreationTime.ToString("g"), $"{file.Length / 1024:N0} KB");
                        }
                        AnsiConsole.Write(backupTable);
                    }
                }
                WaitForInput();
                break;
            case MenuAction.OpenFolder:
                var folderChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string> { CancelResult = () => CancelChoice }
                        .Title("Select a folder to open in your file manager: [grey](Esc to cancel)[/]")
                        .AddChoices("Profile Folder", "Install Folder", "Backup Folder", "Config Folder", CancelChoice));

                if (folderChoice != "Cancel")
                {
                    string path = folderChoice switch
                    {
                        "Profile Folder" => config.ProfilePath,
                        "Install Folder" => config.InstallPath,
                        "Backup Folder" => config.BackupDirectory,
                        "Config Folder" => Path.GetDirectoryName(configPath) ?? "",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(path)) OpenPath(path);
                }
                break;
            case MenuAction.ReconfigureSettings:
                var newConfigRes = RunSetup(configPath);
                if (newConfigRes.IsSuccess) config = newConfigRes.Value!;
                break;
            case MenuAction.Exit:
                return (false, config);
        }
        return (true, config);
    }

    private static void WaitForInput()
    {
        AnsiConsole.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private static void OpenPath(string path)
    {
        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Path does not exist: {path}[/]");
            WaitForInput();
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start("explorer", $"\"{path}\"");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))  // hide QDBusError:  Could not register app ID messages
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(path);
                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", $"\"{path}\"");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to open folder: {ex.Message}[/]");
            WaitForInput();
        }
    }

    private enum MenuAction
    {
        BackupProfile,
        RestoreProfile,
        ViewBackups,
        OpenFolder,
        ReconfigureSettings,
        Exit
    }

    private static string GetMenuDescription(MenuAction action) => action switch
    {
        MenuAction.BackupProfile => "Backup Profile",
        MenuAction.RestoreProfile => "Restore Profile",
        MenuAction.ViewBackups => "View Backups",
        MenuAction.OpenFolder => "Open Folder",
        MenuAction.ReconfigureSettings => "Reconfigure Settings",
        MenuAction.Exit => "Exit",
        _ => action.ToString()
    };

    private const string CancelChoice = "Cancel";
}