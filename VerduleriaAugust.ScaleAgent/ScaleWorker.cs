using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent;

public sealed class ScaleWorker(
    IScaleReader reader,
    ScaleApiClient apiClient,
    IOptions<ScaleOptions> options,
    ILogger<ScaleWorker> logger) : BackgroundService
{
    private readonly ScaleOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agente iniciado en modo {Mode}.", _options.Mode);
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMilliseconds(_options.PollIntervalMilliseconds);
            try
            {
                var reading = await reader.ReadAsync(stoppingToken);
                if (reading is not null)
                {
                    await apiClient.SendAsync(reading, stoppingToken);
                    logger.LogDebug("Peso enviado: {WeightKg} kg, estable: {IsStable}.",
                        reading.WeightKg, reading.IsStable);
                }

                if (consecutiveFailures > 0)
                {
                    logger.LogInformation(
                        "Conexión recuperada después de {FailureCount} intentos fallidos.",
                        consecutiveFailures);
                    consecutiveFailures = 0;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                consecutiveFailures++;
                delay = RetryDelayCalculator.Calculate(
                    consecutiveFailures,
                    _options.PollIntervalMilliseconds,
                    _options.MaxRetryDelaySeconds);

                if (consecutiveFailures == 1)
                {
                    logger.LogWarning(exception,
                        "Se perdió la comunicación con la balanza o la API. " +
                        "El agente seguirá intentando automáticamente.");
                }
                else
                {
                    logger.LogDebug(exception,
                        "Reintento {FailureCount} fallido; próximo intento en {DelaySeconds:0.###} s.",
                        consecutiveFailures, delay.TotalSeconds);
                }
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await reader.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
