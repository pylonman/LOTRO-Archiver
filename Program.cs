using LotroArchiver;
using Spectre.Console;

// Ensure we have a console title
if (!Console.IsOutputRedirected)
{
    Console.Title = "LotroArchiver";
}

var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LotroArchiver");
var configPath = Path.Combine(appDataPath, "config.json");

// Ensure app dir exists
if (!Directory.Exists(appDataPath)) Directory.CreateDirectory(appDataPath);

var configResult = Configuration.Load(configPath);

// Auto mode
if (args.Contains("-auto", StringComparer.OrdinalIgnoreCase))
{
    if (!configResult.IsSuccess)
    {
        Console.Error.WriteLine("Error: Configuration not found. Please run the tool interactively first to setup paths.");
        Environment.Exit(1);
    }

    if (GameProcess.IsRunning())
    {
        Console.Error.WriteLine("Backup skipped: LOTRO is currently running. Close the game and try again.");
        Environment.Exit(2);
    }

    Console.WriteLine("Starting auto-backup...");
    var backupResult = Archiver.Backup(configResult.Value!);
    if (backupResult.IsSuccess)
    {
        Console.WriteLine($"Backup completed successfully: {backupResult.Value}");
        Environment.Exit(0);
    }

    Console.Error.WriteLine($"Backup failed: {backupResult.Error}");
    Environment.Exit(1);
}

// Interactive Mode
if (!configResult.IsSuccess)
{
    try
    {
        configResult = Menu.RunSetup(configPath);
    }
    catch (InvalidOperationException ex)
    {
        AnsiConsole.MarkupLine($"[red]Setup failed: {ex.Message}[/]");
        Environment.Exit(1);
    }
}

if (configResult.IsSuccess)
{
    if (!AnsiConsole.Profile.Capabilities.Interactive)
    {
        AnsiConsole.MarkupLine("[yellow]Non-interactive mode detected. Exiting. Use '-auto' to run backups.[/]");
        Environment.Exit(0);
    }

    Menu.ShowMainMenu(configResult.Value!, configPath);
}
else
{
    AnsiConsole.MarkupLine($"[red]Failed to load configuration: {configResult.Error}[/]");
}
