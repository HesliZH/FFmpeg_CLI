using MeuFFmpeg.Services;

Console.Title = "MeuFFmpeg - CLI pessoal";
var ffmpeg = new FFmpegService();

while (true)
{
    Console.Clear();
    Console.WriteLine("==================================");
    Console.WriteLine("          MeuFFmpeg");
    Console.WriteLine("==================================");
    Console.WriteLine("1. Cortar vídeo");
    Console.WriteLine("2. Verificar FFmpeg");
    Console.WriteLine("0. Sair");
    Console.Write("\nEscolha uma opção: ");

    var option = Console.ReadLine()?.Trim();

    switch (option)
    {
        case "1":
            await CutVideoAsync(ffmpeg);
            break;
        case "2":
            ffmpeg.ShowVersion();
            Pause();
            break;
        case "0":
            return;
        default:
            Console.WriteLine("\nOpção inválida.");
            Pause();
            break;
    }
}

static async Task CutVideoAsync(FFmpegService ffmpeg)
{
    Console.Clear();
    Console.WriteLine("========== Cortar vídeo ==========\n");

    Console.Write("Caminho do vídeo de entrada: ");
    var input = CleanPath(Console.ReadLine());

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

    Console.Write("Caminho do arquivo de saída (Enter para sugerir um nome): ");
    var output = CleanPath(Console.ReadLine());

    if (string.IsNullOrWhiteSpace(output))
    {
        var folder = Path.GetDirectoryName(input)!;
        var name = Path.GetFileNameWithoutExtension(input);
        var ext = Path.GetExtension(input);
        output = Path.Combine(folder, $"{name}_corte{ext}");
        Console.WriteLine($"Saída: {output}");
    }

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

static string CleanPath(string? value)
{
    var result = value?.Trim() ?? "";
    if (result.Length >= 2 && result.StartsWith('"') && result.EndsWith('"'))
        result = result[1..^1];
    return result;
}

static void Pause()
{
    Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
    Console.ReadKey(true);
}
