using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;

namespace WindowsAudioSettingsEnforcer;

public sealed class SettingsWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _lock = new();

    public SettingsWriter(IHostEnvironment environment)
    {
        _path = Path.Combine(environment.ContentRootPath, "appsettings.json");
    }

    public void Save(int volumePercent, bool mute)
    {
        lock (_lock)
        {
            var root = File.Exists(_path)
                ? JsonNode.Parse(File.ReadAllText(_path)) as JsonObject ?? new JsonObject()
                : new JsonObject();

            if (root[AudioOptions.SectionName] is not JsonObject audio)
            {
                audio = new JsonObject();
                root[AudioOptions.SectionName] = audio;
            }

            audio[nameof(AudioOptions.VolumePercent)] = Math.Clamp(volumePercent, 0, 100);
            audio[nameof(AudioOptions.Mute)] = mute;

            File.WriteAllText(_path, root.ToJsonString(WriteOptions));
        }
    }
}
