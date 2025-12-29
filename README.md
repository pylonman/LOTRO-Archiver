# LOTRO Archiver

**Version:** 1.0

LOTRO Archiver is a cross-platform command-line tool designed to easily backup and restore *The Lord of the Rings Online* user profiles (keymaps, layout, preferences, etc.).

## Features

- **Cross-Platform:** Works on Windows and Linux (including Steam Deck/Proton).
- **Auto-Detection:** Automatically finds LOTRO profile directories and Steam installation paths.
- **Backup:** Creates timestamped ZIP archives of your profile data.
- **Restore:** Easily restore previous configurations from backups.
- **Interactive Mode:** User-friendly terminal menu powered by Spectre.Console.
- **Automation:** Supports a `-auto` flag for scripted/scheduled backups.

## Installation

Download the latest binary for your operating system from the Releases page.

### Windows
1. Download `LotroArchiver-win-x64.zip`.
2. Extract and run `LotroArchiver.exe`.

### Linux / Steam Deck
1. Download `LotroArchiver-linux-x64.zip`.
2. Extract the file.
3. Make executable: `chmod +x LotroArchiver`.
4. Run: `./LotroArchiver`.

## Usage

### Interactive Mode
Simply run the application without arguments. It will guide you through the setup process if run for the first time, detecting your game paths.

Once configured, the main menu allows you to:
- Backup Profile
- Restore Profile
- View Backups
- Open relevant folders
- Reconfigure settings

### Automated Backups
You can run the tool with the `-auto` flag to perform a backup immediately without user interaction. This is useful for scheduling tasks (e.g., Windows Task Scheduler or Cron).

```bash
./LotroArchiver -auto
```

## License

MIT