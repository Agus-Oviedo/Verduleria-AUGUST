using Microsoft.Extensions.Options;
using VerduleriaAugust.ScaleAgent;

var builder = Host.CreateApplicationBuilder(args);
var sharedSettingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "VerduleriaAugust", "ScaleAgent", "agent.settings.json");
builder.Configuration.AddJsonFile(sharedSettingsPath, optional: true, reloadOnChange: true);

if (Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.AddWindowsService(options =>
        options.ServiceName = "Augustu - Agente de balanza");
}

builder.Services
    .AddOptions<ScaleOptions>()
    .Bind(builder.Configuration.GetSection(ScaleOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        options.Mode.Equals("Simulator", StringComparison.OrdinalIgnoreCase) ||
        options.Mode.Equals("Serial", StringComparison.OrdinalIgnoreCase),
        "Scale:Mode debe ser Simulator o Serial.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.ApiKey) ^
        !string.IsNullOrWhiteSpace(options.ApiKeyProtectedFile),
        "Configurá solamente Api:ApiKey o Api:ApiKeyProtectedFile.")
    .ValidateOnStart();

builder.Services.AddSingleton<SystelFrameParser>();
builder.Services.AddSingleton<IApiKeyProvider, DpapiApiKeyProvider>();
builder.Services.AddSingleton<IScaleReader>(services =>
{
    var options = services.GetRequiredService<IOptions<ScaleOptions>>().Value;
    return options.Mode.Equals("Serial", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<SystelSerialScaleReader>(services)
        : ActivatorUtilities.CreateInstance<SimulatedScaleReader>(services);
});
builder.Services.AddHttpClient<ScaleApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<ScaleWorker>();

await builder.Build().RunAsync();
