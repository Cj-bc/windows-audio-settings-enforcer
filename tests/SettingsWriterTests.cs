using System.Text.Json.Nodes;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WindowsAudioSettingsEnforcer;

namespace WindowsAudioSettingsEnforcer.Tests;

public sealed class SettingsWriterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly SettingsWriter _writer;

    public SettingsWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "appsettings.json");
        _writer = new SettingsWriter(new FakeHostEnvironment(_tempDir));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Save_WhenFileDoesNotExist_CreatesFile()
    {
        _writer.Save(75, false);

        Assert.True(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_WritesVolumeAndMute()
    {
        _writer.Save(75, true);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal(75, (int)json["Audio"]!["VolumePercent"]!);
        Assert.Equal(true, (bool)json["Audio"]!["Mute"]!);
    }

    [Fact]
    public void Save_ClampsVolumeAbove100()
    {
        _writer.Save(150, false);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal(100, (int)json["Audio"]!["VolumePercent"]!);
    }

    [Fact]
    public void Save_ClampsVolumeBelow0()
    {
        _writer.Save(-10, false);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal(0, (int)json["Audio"]!["VolumePercent"]!);
    }

    [Fact]
    public void Save_PreservesOtherSections()
    {
        File.WriteAllText(_settingsPath, """{"Logging":{"LogLevel":{"Default":"Information"}}}""");

        _writer.Save(50, false);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal("Information", (string?)json["Logging"]!["LogLevel"]!["Default"]);
    }

    [Fact]
    public void Save_UpdatesExistingAudioSection()
    {
        File.WriteAllText(_settingsPath, """{"Audio":{"VolumePercent":30,"Mute":false}}""");

        _writer.Save(80, true);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal(80, (int)json["Audio"]!["VolumePercent"]!);
        Assert.Equal(true, (bool)json["Audio"]!["Mute"]!);
    }

    [Fact]
    public void Save_WhenFileHasNoAudioSection_AddsAudioSection()
    {
        File.WriteAllText(_settingsPath, """{"Logging":{}}""");

        _writer.Save(60, false);

        var json = JsonNode.Parse(File.ReadAllText(_settingsPath))!;
        Assert.Equal(60, (int)json["Audio"]!["VolumePercent"]!);
    }

    private sealed class FakeHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Test";
    }
}
