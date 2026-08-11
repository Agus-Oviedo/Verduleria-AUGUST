using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent;

public sealed class SimulatedScaleReader(IOptions<ScaleOptions> options) : IScaleReader
{
    private readonly ScaleOptions _options = options.Value;

    public Task<ScaleReading?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<ScaleReading?>(new(
            Guid.NewGuid(),
            _options.SimulatorWeightKg,
            _options.SimulatorStable,
            DateTimeOffset.UtcNow,
            $"SIM:{_options.SimulatorWeightKg:0.000}"));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
