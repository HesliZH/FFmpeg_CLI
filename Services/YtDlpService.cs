using System.Diagnostics;

namespace MeuFFmpeg.Services;

public sealed class YtDlpService
{
    private const string DefaultYtDlpFolder = "C:\\yt-dlp";
    private readonly string _ytDlpPath;

    public YtDlpService()
    {
        _ytDlpPath = ResolveYtDlpPath();
    }

    private static string ResolveYtDlpPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(DefaultYtDlpFolder, "yt-dlp.exe")
        };

        var explicitPath = Environment.GetEnvironmentVariable("YTDLP_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            candidates.Add(explicitPath);

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(Path.Combine(entry, "yt-dlp.exe"));
                candidates.Add(entry);
            }
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalized = candidate.Trim();
            if (File.Exists(normalized))
                return normalized;

            if (Directory.Exists(normalized))
            {
                var directExe = Path.Combine(normalized, "yt-dlp.exe");
                if (File.Exists(directExe))
                    return directExe;
            }
        }

        return "yt-dlp";
    }

    public async Task<int> DownloadVideoAsync(string url, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Informe a URL do vídeo.");

        var fullOutput = Path.GetFullPath(outputPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Melhor combinação vídeo+áudio compatível com MP4, garantindo que o FFmpeg leia sem engasgos.
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/b");
        startInfo.ArgumentList.Add("--merge-output-format");
        startInfo.ArgumentList.Add("mp4");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(fullOutput);
        startInfo.ArgumentList.Add(url);

        using var process = Process.Start(startInfo);
        if (process is null)
            return -1;

        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}