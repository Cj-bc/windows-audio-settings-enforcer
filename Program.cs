using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WindowsAudioSettingsEnforcer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "AudioSettingsEnforcer");
builder.Services.Configure<AudioOptions>(builder.Configuration.GetSection(AudioOptions.SectionName));
builder.Services.AddHostedService<AudioEnforcerService>();

builder.Build().Run();
