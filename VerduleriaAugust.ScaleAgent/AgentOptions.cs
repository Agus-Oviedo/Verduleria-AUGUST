using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.ScaleAgent;

public sealed class ScaleOptions
{
    public const string SectionName = "Scale";

    [Required]
    public string Mode { get; init; } = "Simulator";

    [Required]
    public string PortName { get; init; } = "COM1";

    [Range(1, 115200)]
    public int BaudRate { get; init; } = 9600;

    [Range(100, 10000)]
    public int PollIntervalMilliseconds { get; init; } = 500;

    [Range(50, 5000)]
    public int ResponseTimeoutMilliseconds { get; init; } = 1000;

    [Range(0, 1000)]
    public int InterByteTimeoutMilliseconds { get; init; } = 20;

    [Range(1, 300)]
    public int MaxRetryDelaySeconds { get; init; } = 30;

    [Range(0, 1000)]
    public decimal SimulatorWeightKg { get; init; } = 1.250m;

    public bool SimulatorStable { get; init; } = true;
}

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    [Required, Url]
    public string BaseUrl { get; init; } = "https://localhost:7001";

    [Range(1, int.MaxValue)]
    public int ScaleId { get; init; } = 1;

    public string? ApiKey { get; init; }

    public string? ApiKeyProtectedFile { get; init; }
}
