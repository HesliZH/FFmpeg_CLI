using System.Diagnostics;
using System.Globalization;

namespace MeuFFmpeg.Services;

public sealed class FFmpegService
{
    private const string DefaultFFmpegFolder = "C:\\ffmpeg\\bin";
    private readonly string _ffmpegPath;

    public FFmpegService()
    {
        _ffmpegPath = ResolveFFmpegPath();
    }

    private static string ResolveFFmpegPath()
    {
        var candidates = new List<string>
        {
            Path.Combine(DefaultFFmpegFolder, "ffmpeg.exe")
        };

        var explicitFfmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(explicitFfmpegPath))
            candidates.Add(explicitFfmpegPath);

        var ffmpegBinPath = Environment.GetEnvironmentVariable("FFMPEG_BIN");
        if (!string.IsNullOrWhiteSpace(ffmpegBinPath))
        {
            candidates.Add(Path.Combine(ffmpegBinPath, "ffmpeg.exe"));
            candidates.Add(ffmpegBinPath);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(Path.Combine(entry, "ffmpeg.exe"));
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
                var directExe = Path.Combine(normalized, "ffmpeg.exe");
                if (File.Exists(directExe))
                    return directExe;
            }
        }

        return "ffmpeg";
    }

    public void ShowVersion()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-version");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.WriteLine("Não foi possível iniciar o FFmpeg.");
                return;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine(string.IsNullOrWhiteSpace(stdout) ? stderr : stdout);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            Console.WriteLine("FFmpeg não encontrado.");
            Console.WriteLine("Coloque ffmpeg.exe na pasta tools ou adicione o FFmpeg ao PATH do Windows.");
        }
    }

    public async Task<int> CutAsync(
        string inputPath,
        string outputPath,
        string start,
        string duration,
        bool precise)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("O vídeo de entrada não foi encontrado.", inputPath);

        var startSeconds = ParseTime(start, "início");
        var durationSeconds = ParseTime(duration, "duração");

        if (durationSeconds <= 0)
            throw new ArgumentException("A duração precisa ser maior que zero.");

        var fullOutput = Path.GetFullPath(outputPath);
        var fullInput = Path.GetFullPath(inputPath);

        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O arquivo de saída não pode ser o mesmo arquivo de entrada.");

        var outputDirectory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Para copy, colocar -ss antes de -i costuma ser mais rápido.
        if (!precise)
            startInfo.ArgumentList.Add("-ss");

        if (!precise)
            startInfo.ArgumentList.Add(FormatSeconds(startSeconds));

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(fullInput);

        // Para o modo preciso, buscar após abrir a entrada faz o FFmpeg decodificar até o ponto pedido.
        if (precise)
        {
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(FormatSeconds(startSeconds));
        }

        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(FormatSeconds(durationSeconds));

        if (precise)
        {
            startInfo.ArgumentList.Add("-c:v");
            startInfo.ArgumentList.Add("libx264");
            startInfo.ArgumentList.Add("-preset");
            startInfo.ArgumentList.Add("medium");
            startInfo.ArgumentList.Add("-crf");
            startInfo.ArgumentList.Add("18");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            startInfo.ArgumentList.Add("-b:a");
            startInfo.ArgumentList.Add("192k");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("copy");
        }

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(fullOutput);

        using var process = new Process { StartInfo = startInfo };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Console.WriteLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();

        // Ler stdout evita que o buffer bloqueie o processo.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stdoutTask;

        return process.ExitCode;
    }

    private static double ParseTime(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Informe o {fieldName}.");

        // Aceita segundos (90.5) ou HH:MM:SS / MM:SS.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            if (seconds < 0)
                throw new ArgumentException($"O {fieldName} não pode ser negativo.");
            return seconds;
        }

        var parts = value.Split(':');
        if (parts.Length is < 2 or > 3 ||
            !parts.All(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
        {
            throw new ArgumentException($"Formato inválido para {fieldName}. Use HH:MM:SS ou segundos, por exemplo 90 ou 00:01:30.");
        }

        var numbers = parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        if (numbers.Any(n => n < 0) || numbers[^1] >= 60 || (parts.Length == 3 && numbers[1] >= 60))
            throw new ArgumentException($"Valor de tempo inválido para {fieldName}.");

        return parts.Length == 3
            ? numbers[0] * 3600 + numbers[1] * 60 + numbers[2]
            : numbers[0] * 60 + numbers[1];
    }

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);
}
