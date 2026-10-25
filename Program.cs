using MeuFFmpeg;
using MeuFFmpeg.Services;

Console.Title = "MeuFFmpeg - CLI pessoal";
var ffmpeg = new FFmpegService();
var settings = AppSettings.Load();

while (true)
{
    Console.Clear();
    Console.WriteLine("==================================");
    Console.WriteLine("          MeuFFmpeg");
    Console.WriteLine("==================================");
    Console.WriteLine("1. Cortar vídeo");
    Console.WriteLine("2. Verificar FFmpeg");
    Console.WriteLine("==================================");
    Console.WriteLine("99. Configurações");
    Console.WriteLine("100. Sobre");
    Console.WriteLine("==================================");
    Console.WriteLine("0. Sair");
    Console.Write("\nEscolha uma opção: ");

    var option = Console.ReadLine()?.Trim();

    switch (option)
    {
        case "1":
            await CutVideoAsync(ffmpeg, settings);
            break;
        case "2":
            ffmpeg.ShowVersion();
            Pause();
            break;
        case "99":
            settings = ConfigureSettings(settings);
            break;
        case "100":
            ShowAbout();
            break;
        case "0":
            return;
        default:
            Console.WriteLine("\nOpção inválida.");
            Pause();
            break;
    }
}

static async Task CutVideoAsync(FFmpegService ffmpeg, AppSettings settings)
{
    Console.Clear();
    Console.WriteLine("========== Cortar vídeo ==========\n");

    var defaultInputFolder = !string.IsNullOrWhiteSpace(settings.DefaultInputFolder)
        ? settings.DefaultInputFolder
        : Directory.GetCurrentDirectory();

    Console.Write($"Caminho do vídeo de entrada (padrão: {defaultInputFolder}): ");
    var rawInput = Console.ReadLine();
    var input = string.IsNullOrWhiteSpace(rawInput)
        ? SearchInputPathInDefaultFolder(defaultInputFolder)
        : ResolvePath(rawInput, defaultInputFolder);

    if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
    {
        Console.WriteLine("\nArquivo não encontrado.");
        Pause();
        return;
    }

    Console.Write("Início do corte (HH:MM:SS ou segundos; ex. 00:01:30): ");
    var start = Console.ReadLine()?.Trim() ?? "";

    Console.Write("Duração do trecho (HH:MM:SS ou segundos; ex. 00:00:20): ");
    var duration = Console.ReadLine()?.Trim() ?? "";

    var defaultOutputFolder = !string.IsNullOrWhiteSpace(settings.DefaultOutputFolder)
        ? settings.DefaultOutputFolder
        : Path.GetDirectoryName(input)!;

    Console.Write($"Caminho do arquivo de saída (padrão: {defaultOutputFolder}): ");
    var rawOutput = Console.ReadLine();
    var output = string.IsNullOrWhiteSpace(rawOutput)
        ? SuggestOutputPath(input, defaultOutputFolder)
        : ResolvePath(rawOutput, defaultOutputFolder);

    Console.WriteLine("\nModo de corte:");
    Console.WriteLine("1. Rápido, sem recodificar (pode variar alguns quadros no início)");
    Console.WriteLine("2. Preciso, recodificando para H.264/AAC (gera MP4)");
    Console.Write("Escolha (1 ou 2): ");
    var mode = Console.ReadLine()?.Trim();

    if (mode is not ("1" or "2"))
    {
        Console.WriteLine("\nModo inválido.");
        Pause();
        return;
    }

    if (mode == "2")
    {
        var outputFolder = Path.GetDirectoryName(output);
        var outputName = Path.GetFileNameWithoutExtension(output);
        output = Path.Combine(
            string.IsNullOrWhiteSpace(outputFolder) ? Directory.GetCurrentDirectory() : outputFolder,
            outputName + "_preciso.mp4");
    }

    try
    {
        Console.WriteLine("\nIniciando FFmpeg...\n");
        var exitCode = await ffmpeg.CutAsync(input, output, start, duration, mode == "2");

        Console.WriteLine(exitCode == 0
            ? $"\nCorte concluído!\nArquivo: {output}"
            : $"\nO FFmpeg terminou com código {exitCode}. Confira as mensagens acima.");
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"\nEntrada inválida: {ex.Message}");
    }
    catch (FileNotFoundException ex)
    {
        Console.WriteLine($"\n{ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nErro ao executar o corte: {ex.Message}");
    }

    Pause();
}

static AppSettings ConfigureSettings(AppSettings currentSettings)
{
    Console.Clear();
    Console.WriteLine("========== Configurações ==========");
    Console.WriteLine($"Arquivo INI: {AppSettings.DefaultConfigPath}\n");

    var inputFolder = PromptForAbsoluteDirectory(
        "Pasta de entrada padrão",
        currentSettings.DefaultInputFolder,
        "C:\\Videos");

    var outputFolder = PromptForAbsoluteDirectory(
        "Pasta de saída padrão",
        currentSettings.DefaultOutputFolder,
        "C:\\Videos\\Saida");

    var settings = new AppSettings
    {
        DefaultInputFolder = inputFolder,
        DefaultOutputFolder = outputFolder
    };

    AppSettings.Save(settings);
    Console.WriteLine("\nConfigurações salvas com sucesso.");
    Pause();
    return settings;
}

static string PromptForAbsoluteDirectory(string label, string currentValue, string suggestedValue)
{
    while (true)
    {
        var currentText = string.IsNullOrWhiteSpace(currentValue) ? suggestedValue : currentValue;
        Console.Write($"{label} [{currentText}] (caminho absoluto): ");

        var entered = CleanPath(Console.ReadLine());
        if (string.IsNullOrWhiteSpace(entered))
        {
            if (!string.IsNullOrWhiteSpace(currentValue))
                return Path.GetFullPath(currentValue);

            var suggested = Path.GetFullPath(suggestedValue);
            Directory.CreateDirectory(suggested);
            return suggested;
        }

        if (!AppSettings.IsValidAbsoluteDirectory(entered))
        {
            Console.WriteLine("\nO caminho precisa ser absoluto, por exemplo: C:\\Videos ou D:\\Arquivos\\Destino");
            continue;
        }

        var absolute = Path.GetFullPath(entered);
        Directory.CreateDirectory(absolute);
        return absolute;
    }
}

static string ResolvePath(string input, string defaultFolder)
{
    var cleaned = CleanPath(input);
    if (string.IsNullOrWhiteSpace(cleaned))
        return string.Empty;

    if (Path.IsPathRooted(cleaned))
        return Path.GetFullPath(cleaned);

    return Path.GetFullPath(Path.Combine(defaultFolder, cleaned));
}

static string SearchInputPathInDefaultFolder(string defaultFolder)
{
    if (string.IsNullOrWhiteSpace(defaultFolder) || !Directory.Exists(defaultFolder))
        return string.Empty;

    var files = Directory.GetFiles(defaultFolder)
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (files.Count == 0)
        return string.Empty;

    return files[0];
}

static string SuggestOutputPath(string inputPath, string defaultOutputFolder)
{
    if (string.IsNullOrWhiteSpace(inputPath))
        return string.Empty;

    var folder = string.IsNullOrWhiteSpace(defaultOutputFolder)
        ? Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory()
        : defaultOutputFolder;

    var name = Path.GetFileNameWithoutExtension(inputPath);
    var ext = Path.GetExtension(inputPath);
    return Path.Combine(folder, $"{name}_corte{ext}");
}

static string CleanPath(string? value)
{
    var result = value?.Trim() ?? string.Empty;
    if (result.Length >= 2 && result.StartsWith('"') && result.EndsWith('"'))
        result = result[1..^1];
    return result;
}

static void ShowAbout()
{
    Console.Clear();
    Console.WriteLine("========== Sobre ==========");
    Console.WriteLine("MeuFFmpeg - CLI pessoal para recortes e validações de FFmpeg.");
    Console.WriteLine("Configuração salva em: " + AppSettings.DefaultConfigPath);
    Pause();
}

static void Pause()
{
    Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
    Console.ReadKey(true);
}
