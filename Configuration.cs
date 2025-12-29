using System.Text.Json;
using System.Text.Json.Serialization;

namespace LotroArchiver;

public record AppConfig(string ProfilePath, string InstallPath, string BackupDirectory);

[JsonSerializable(typeof(AppConfig))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}

public static class Configuration
{
    public static Result<AppConfig> Load(string path)
    {
        if (!File.Exists(path)) return Result<AppConfig>.Failure("Config file not found.");
        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
            return config is not null ? Result<AppConfig>.Success(config) : Result<AppConfig>.Failure("Failed to deserialize config.");
        }
        catch (Exception ex)
        {
            return Result<AppConfig>.Failure(ex.Message);
        }
    }

    public static Result Save(string path, AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, AppJsonContext.Default.AppConfig);
            File.WriteAllText(path, json);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}