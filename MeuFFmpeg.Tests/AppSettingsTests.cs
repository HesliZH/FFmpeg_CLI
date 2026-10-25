using MeuFFmpeg;
using Xunit;

namespace MeuFFmpeg.Tests;

public class AppSettingsTests
{
    [Fact]
    public void IsValidAbsoluteDirectory_RejectsRelativePath()
    {
        Assert.False(AppSettings.IsValidAbsoluteDirectory("Videos"));
        Assert.True(AppSettings.IsValidAbsoluteDirectory(@"C:\Videos"));
    }

    [Fact]
    public void SaveAndLoad_PersistsDefaultFolders()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempPath);
        var configPath = Path.Combine(tempPath, AppSettings.FileName);

        var settings = new AppSettings
        {
            DefaultInputFolder = @"C:\Videos\Entrada",
            DefaultOutputFolder = @"D:\Videos\Saida"
        };

        AppSettings.Save(settings, configPath);
        var loaded = AppSettings.Load(configPath);

        Assert.Equal(settings.DefaultInputFolder, loaded.DefaultInputFolder);
        Assert.Equal(settings.DefaultOutputFolder, loaded.DefaultOutputFolder);

        Directory.Delete(tempPath, true);
    }
}
