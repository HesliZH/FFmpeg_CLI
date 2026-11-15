using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MeuFFmpeg.Services;

public sealed class YtDlpService
{
    private const string DefaultYtDlpFolder = "C:\\yt-dlp";
    private readonly string _ytDlpPath;

    private static readonly Regex DownloadProgressRegex = new(
        @"\[download\]\s+(\d+(?:\.\d+)?)%\s*(.*)",
        RegexOptions.Compiled);

    // Resolução máxima baixada: 480p é mais que suficiente para o VHS (CRT 4:3) e reduz o tamanho/banda.
    private const string MaxHeightFilter = "[height<=480]";

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

    public async Task<int> DownloadVideoAsync(
        string url,
        string outputPath)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Informe a URL do vídeo.");

        var fullOutput = Path.GetFullPath(outputPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Melhor combinação vídeo+áudio compatível com MP4, limitada a 480p.
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add($"bv*{MaxHeightFilter}[ext=mp4]+ba[ext=m4a]/b{MaxHeightFilter}[ext=mp4]/b{MaxHeightFilter}");
        // Fragmenta o download em 4 conexões simultâneas (DASH/HLS), acelerando bastante a velocidade.
        startInfo.ArgumentList.Add("-N");
        startInfo.ArgumentList.Add("4");
        // User-Agent de navegador comum evita estrangulamento de banda pelo YouTube.
        startInfo.ArgumentList.Add("--user-agent");
        startInfo.ArgumentList.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        // Finge ser o app oficial do Android: o YouTube entrega banda cheia nesse player client.
        startInfo.ArgumentList.Add("--extractor-args");
        startInfo.ArgumentList.Add("youtube:player_client=android");
        // Usa o Node.js instalado como runtime de JavaScript (sem ele, a extração do YouTube
        // fica limitada e alguns formatos somem).
        startInfo.ArgumentList.Add("--js-runtimes");
        startInfo.ArgumentList.Add("node");
        startInfo.ArgumentList.Add("--merge-output-format");
        startInfo.ArgumentList.Add("mp4");
        // Cada atualização de progresso do yt-dlp vira uma linha, permitindo ler e desenhar a barra.
        startInfo.ArgumentList.Add("--newline");

        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(fullOutput);
        startInfo.ArgumentList.Add(url);

        using var process = new Process { StartInfo = startInfo };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            var match = DownloadProgressRegex.Match(e.Data);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
            {
                var status = Regex.Replace(match.Groups[2].Value.Trim(), @"\s+", " ");
                DrawDownloadProgressBar(percentage, status);
                return;
            }

            // Só repassa linhas informativas do próprio yt-dlp; o log cru do FFmpeg interno
            // (merge/remux) é descartado para não tomar a tela.
            if (IsInformativeLine(e.Data))
                Console.WriteLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        Console.WriteLine();
        return process.ExitCode;
    }

    private static void DrawDownloadProgressBar(double percentage, string status)
    {
        // Ajusta a largura para não quebrar a linha em janelas estreitas.
        var barWidth = Math.Clamp(Console.WindowWidth - 40, 10, 40);
        var filled = (int)Math.Round(percentage / 100.0 * barWidth);
        var bar = new string('█', filled) + new string('░', barWidth - filled);

        Console.Write($"\rBaixando: [{bar}] {percentage:0.0}% {status}");
    }

    private static bool IsInformativeLine(string line)
    {
        if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return line.StartsWith("[youtube]", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[info]", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[download] Destination:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[Merger]", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[ExtractAudio]", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[FixupM3u8]", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("[VideoRemuxer]", StringComparison.OrdinalIgnoreCase);
    }
}