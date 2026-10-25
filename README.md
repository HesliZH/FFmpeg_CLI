# MeuFFmpeg

CLI interativa em C#/.NET 8 para executar operações comuns do FFmpeg no Windows.

## Requisitos

- .NET 8 SDK para compilar.
- FFmpeg para Windows.
- Não precisa de Docker nem de conexão com a internet para executar.

## Instalação do FFmpeg

Baixe uma compilação confiável do FFmpeg para Windows e copie `ffmpeg.exe` para:

```text
MeuFFmpeg\tools\ffmpeg.exe
```

A estrutura esperada é:

```text
MeuFFmpeg/
  MeuFFmpeg.csproj
  Program.cs
  Services/
    FFmpegService.cs
  tools/
    ffmpeg.exe
```

Também é possível deixar o FFmpeg instalado em outro lugar e adicionar a pasta dele ao `PATH` do Windows. O programa tenta primeiro `tools\ffmpeg.exe` ao lado do executável e, se não existir, usa `ffmpeg` do PATH.

## Executar em desenvolvimento

Abra o CMD na pasta do projeto e execute:

```cmd
dotnet run
```

## Publicar como executável local

Para gerar um executável Windows x64:

```cmd
dotnet publish -c Release -r win-x64 --self-contained true
```

O resultado ficará em `bin\Release\net8.0\win-x64\publish\`.

Copie a pasta `tools` para dentro da pasta `publish` para que o programa encontre `tools\ffmpeg.exe`.

## Como cortar

1. Escolha `1. Cortar vídeo`.
2. Informe o caminho do vídeo, por exemplo `D:\Videos\gameplay.wmv`.
3. Informe o início e a duração, por exemplo `00:01:30` e `00:00:20`.
4. Informe o destino ou pressione Enter para sugerir um nome.
5. Escolha o modo:
   - **Rápido:** usa `-c copy`, sem recodificar. É muito rápido, mas o ponto inicial pode variar conforme os keyframes.
   - **Preciso:** recodifica para H.264/AAC em MP4. É mais lento e a saída é criada com o sufixo `_preciso.mp4`.

O programa não apaga o arquivo original. Se já existir um arquivo de saída com o mesmo nome, o FFmpeg irá sobrescrevê-lo (`-y`).

## Observações

- O modo preciso exige que a compilação do FFmpeg inclua `libx264`.
- O modo rápido preserva os codecs originais; a extensão escolhida deve ser compatível com o conteúdo.
- O código usa `ProcessStartInfo.ArgumentList`, evitando montar uma linha de comando vulnerável a problemas de aspas nos caminhos.
