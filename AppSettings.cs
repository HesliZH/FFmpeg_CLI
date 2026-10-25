namespace MeuFFmpeg;

public sealed class AppSettings
{
    public const string FileName = "MeuFFmpeg.ini";

    public string DefaultInputFolder { get; set; } = string.Empty;
    public string DefaultOutputFolder { get; set; } = string.Empty;

    public static string DefaultConfigPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static AppSettings Load(string? configPath = null)
    {
        var path = string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : configPath;
        var settings = new AppSettings();

        if (!File.Exists(path))
            return settings;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            var index = trimmed.IndexOf('=');
            if (index < 0)
                continue;

            var key = trimmed[..index].Trim();
            var value = trimmed[(index + 1)..].Trim();

            switch (key)
            {
                case "InputFolder":
                    settings.DefaultInputFolder = value;
                    break;
                case "OutputFolder":
                    settings.DefaultOutputFolder = value;
                    break;
            }
        }

        return settings;
    }

    public static void Save(AppSettings settings, string? configPath = null)
    {
        var path = string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : configPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var normalizedInput = NormalizeAbsoluteDirectory(settings.DefaultInputFolder);
        var normalizedOutput = NormalizeAbsoluteDirectory(settings.DefaultOutputFolder);

        var content = new[]
        {
            "[Paths]",
            $"InputFolder={normalizedInput}",
            $"OutputFolder={normalizedOutput}"
        };

        File.WriteAllLines(path, content);
    }

    public static bool IsValidAbsoluteDirectory(string? value)
    {
        var cleaned = CleanPath(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        if (!Path.IsPathRooted(cleaned))
            return false;

        return true;
    }

    public static string NormalizeAbsoluteDirectory(string? value, string? defaultBaseFolder = null)
    {
        var cleaned = CleanPath(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        if (!Path.IsPathRooted(cleaned) && !string.IsNullOrWhiteSpace(defaultBaseFolder))
            cleaned = Path.Combine(defaultBaseFolder, cleaned);

        return Path.GetFullPath(cleaned);
    }

    private static string CleanPath(string? value)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length >= 2 && result.StartsWith('"') && result.EndsWith('"'))
            result = result[1..^1];

        return result;
    }
}
