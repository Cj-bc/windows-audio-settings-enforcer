using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WindowsAudioSettingsEnforcer;

using var singleInstanceMutex = new Mutex(initiallyOwned: true, "Cj-bc.AudioSettingsEnforcer", out var isFirstInstance);
if (!isFirstInstance)
{
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection(AudioOptions.SectionName));
builder.Services.AddSingleton<EnforcementState>();
builder.Services.AddSingleton<SettingsWriter>();
builder.Services.AddHostedService<AudioEnforcerService>();
builder.Services.AddHostedService<TrayIconService>();

builder.Build().Run();
