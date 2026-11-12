using MeuFFmpeg;
using MeuFFmpeg.Services;

Console.Title = "MeuFFmpeg - CLI pessoal";
var ffmpeg = new FFmpegService();
var settings = AppSettings.Load();

// Na primeira execução, cria o INI já preenchido com os valores padrão.
if (!File.Exists(AppSettings.DefaultConfigPath))
{
    settings = AppSettings.CreateDefault();
    AppSettings.Save(settings);
}

while (true)
{
    Console.Clear();
    Console.WriteLine("==================================");
    Console.WriteLine("          MeuFFmpeg");
    Console.WriteLine("==================================");
    Console.WriteLine("1. Cortar vídeo");
    Console.WriteLine("2. Verificar FFmpeg");
    Console.WriteLine("3. Converter para VHS");
    Console.WriteLine("4. Concatenar vídeos");
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
        case "3":
            await ConvertToVhsAsync(ffmpeg, settings);
            break;
        case "4":
            await ConcatVideosAsync(ffmpeg, settings);
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

static async Task ConvertToVhsAsync(FFmpegService ffmpeg, AppSettings settings)
{
    Console.Clear();
    Console.WriteLine("========== Converter para VHS ==========\n");

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

    Console.WriteLine("\nPadrão de vídeo 4:3:");
    Console.WriteLine("1. NTSC 640x480 (padrão)");
    Console.WriteLine("2. PAL 768x576");
    Console.Write("Escolha (1 ou 2) [1]: ");
    var standard = Console.ReadLine()?.Trim();
    var (width, height) = standard == "2" ? (768, 576) : (640, 480);

    var defaultOutputFolder = !string.IsNullOrWhiteSpace(settings.DefaultOutputFolder)
        ? settings.DefaultOutputFolder
        : Path.GetDirectoryName(input)!;

    Console.Write($"Caminho do arquivo de saída (padrão: {defaultOutputFolder}): ");
    var rawOutput = Console.ReadLine();
    var output = string.IsNullOrWhiteSpace(rawOutput)
        ? SuggestOutputPath(input, defaultOutputFolder, "_vhs", ".mp4")
        : ResolvePath(rawOutput, defaultOutputFolder);

    try
    {
        Console.WriteLine("\nIniciando FFmpeg...\n");
        var exitCode = await ffmpeg.ConvertToVhsAsync(input, output, width, height);

        Console.WriteLine(exitCode == 0
            ? $"\nConversão concluída!\nArquivo: {output}"
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
        Console.WriteLine($"\nErro ao converter: {ex.Message}");
    }

    Pause();
}

static async Task ConcatVideosAsync(FFmpegService ffmpeg, AppSettings settings)
{
    Console.Clear();
    Console.WriteLine("========== Concatenar vídeos ==========\n");

    var defaultInputFolder = !string.IsNullOrWhiteSpace(settings.DefaultInputFolder)
        ? settings.DefaultInputFolder
        : Directory.GetCurrentDirectory();

    Console.WriteLine("Intro (opcional). Enter usa a padrão configurada, se houver.");
    Console.Write($"Caminho (padrão: {defaultInputFolder}): ");
    var rawIntro = Console.ReadLine();
    var intro = string.IsNullOrWhiteSpace(rawIntro)
        ? (!string.IsNullOrWhiteSpace(settings.DefaultIntroPath) ? settings.DefaultIntroPath : null)
        : ResolvePath(rawIntro, defaultInputFolder);

    if (intro is not null && !File.Exists(intro))
    {
        Console.WriteLine("\nIntro não encontrada.");
        Pause();
        return;
    }

    var videos = new List<string>();
    Console.WriteLine("\nVídeos na ordem de concatenação (Enter vazio para finalizar):");
    while (true)
    {
        Console.Write("Caminho (ou Enter para finalizar): ");
        var raw = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(raw))
            break;

        var video = ResolvePath(raw, defaultInputFolder);
        if (File.Exists(video))
            videos.Add(video);
        else
            Console.WriteLine("Arquivo não encontrado. Tente novamente.");
    }

    if (videos.Count == 0)
    {
        Console.WriteLine("\nNenhum vídeo informado.");
        Pause();
        return;
    }

    var defaultOutputFolder = !string.IsNullOrWhiteSpace(settings.DefaultOutputFolder)
        ? settings.DefaultOutputFolder
        : Path.GetDirectoryName(videos[0])!;

    Console.Write($"\nCaminho do arquivo de saída (padrão: {defaultOutputFolder}): ");
    var rawOutput = Console.ReadLine();
    var output = string.IsNullOrWhiteSpace(rawOutput)
        ? SuggestOutputPath(videos[0], defaultOutputFolder, "_concat", ".avi")
        : ResolvePath(rawOutput, defaultOutputFolder);

    try
    {
        Console.WriteLine("\nIniciando FFmpeg...\n");
        var exitCode = await ffmpeg.ConcatAsync(output, intro, videos);

        Console.WriteLine(exitCode == 0
            ? $"\nConcatenação concluída!\nArquivo: {output}"
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
        Console.WriteLine($"\nErro ao concatenar: {ex.Message}");
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

    var introPath = PromptForOptionalFile(
        "Intro padrão",
        currentSettings.DefaultIntroPath);

    var settings = new AppSettings
    {
        DefaultInputFolder = inputFolder,
        DefaultOutputFolder = outputFolder,
        DefaultIntroPath = introPath
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

static string PromptForOptionalFile(string label, string currentValue)
{
    var currentText = string.IsNullOrWhiteSpace(currentValue) ? "(nenhuma)" : currentValue;
    Console.WriteLine($"\n{label} [{currentText}] (caminho absoluto; Enter mantém, - limpa): ");

    while (true)
    {
        var entered = CleanPath(Console.ReadLine());
        if (string.IsNullOrWhiteSpace(entered))
            return string.IsNullOrWhiteSpace(currentValue) ? string.Empty : Path.GetFullPath(currentValue);

        if (entered == "-")
            return string.Empty;

        var absolute = Path.GetFullPath(entered);
        if (File.Exists(absolute))
            return absolute;

        Console.WriteLine("\nArquivo não encontrado. Tente novamente (ou - para limpar).");
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

static string SuggestOutputPath(
    string inputPath,
    string defaultOutputFolder,
    string suffix = "_corte",
    string? outputExtension = null)
{
    if (string.IsNullOrWhiteSpace(inputPath))
        return string.Empty;

    var folder = string.IsNullOrWhiteSpace(defaultOutputFolder)
        ? Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory()
        : defaultOutputFolder;

    var name = Path.GetFileNameWithoutExtension(inputPath);
    var ext = outputExtension ?? Path.GetExtension(inputPath);
    return Path.Combine(folder, $"{name}{suffix}{ext}");
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
