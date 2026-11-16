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
            RedirectStandardOutput = true,
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
        startInfo.ArgumentList.Add("--merge-output-format");
        startInfo.ArgumentList.Add("mp4");
        // Cada atualização de progresso do yt-dlp vira uma linha, permitindo ler e desenhar a barra.
        startInfo.ArgumentList.Add("--newline");

        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(fullOutput);
        startInfo.ArgumentList.Add(url);

        using var process = new Process { StartInfo = startInfo };

        // O yt-dlp envia progresso e linhas de info pelo stdout e warnings/erros pelo stderr;
        // ambos os fluxos passam pelo mesmo tratamento, garantindo que a barra seja desenhada.
        process.OutputDataReceived += (_, e) => HandleYtDlpLine(e.Data);
        process.ErrorDataReceived += (_, e) => HandleYtDlpLine(e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();
        Console.WriteLine();
        return process.ExitCode;
    }

    private static void HandleYtDlpLine(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return;

        var match = DownloadProgressRegex.Match(data);
        if (match.Success &&
            double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage))
        {
            var status = Regex.Replace(match.Groups[2].Value.Trim(), @"\s+", " ");
            DrawDownloadProgressBar(percentage, status);
            return;
        }

        // Só repassa linhas informativas do próprio yt-dlp; o log cru do FFmpeg interno
        // (merge/remux) é descartado para não tomar a tela.
        if (IsInformativeLine(data))
        {
            ClearCurrentLine();
            Console.WriteLine(data);
        }
    }

    private static void DrawDownloadProgressBar(double percentage, string status)
    {
        // Largura total da linha, respeitando a janela para não quebrar em telas estreitas.
        var totalWidth = Math.Clamp(Console.WindowWidth - 2, 30, 120);

        // Informação extra (%, velocidade, ETA...) ocupa o restante depois do prefixo e da barra.
        const int fixedPrefix = 13; // "Baixando: [" + "] "
        var barWidth = Math.Clamp(totalWidth / 3, 10, 40);
        var infoMax = Math.Max(0, totalWidth - fixedPrefix - barWidth);
        var info = $"{percentage:0.0}% {status}";
        if (info.Length > infoMax)
            info = info[..infoMax];

        var filled = (int)Math.Round(percentage / 100.0 * barWidth);
        var bar = new string('█', filled) + new string('░', barWidth - filled);

        // Preenche com espaços para apagar resíduos de linhas anteriores mais longas.
        var line = $"Baixando: [{bar}] {info}";
        Console.Write("\r" + line.PadRight(totalWidth));
    }

    // Limpa a linha atual para a mensagem informativa não ficar sobreposta à barra de progresso.
    private static void ClearCurrentLine()
    {
        var width = Math.Clamp(GetSafeConsoleWidth() - 2, 30, 120);
        Console.Write($"\r{new string(' ', width)}\r");
    }

    // Com a saída redirecionada (IDE/sandbox), ler Console.WindowWidth lança exceção;
    // cai para um valor padrão para a barra continuar funcionando.
    private static int GetSafeConsoleWidth()
    {
        try { return Console.WindowWidth; }
        catch { return 80; }
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