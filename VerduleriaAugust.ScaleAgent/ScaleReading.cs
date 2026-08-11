namespace VerduleriaAugust.ScaleAgent;

public sealed record ScaleReading(
    Guid ReadingId,
    decimal WeightKg,
    bool IsStable,
    DateTimeOffset ReadAt,
    string RawData);

public interface IScaleReader : IAsyncDisposable
{
    Task<ScaleReading?> ReadAsync(CancellationToken cancellationToken);
}
