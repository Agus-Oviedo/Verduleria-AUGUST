using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent;

public sealed class ScaleApiClient(
    HttpClient httpClient,
    IOptions<ApiOptions> options,
    IApiKeyProvider apiKeyProvider)
{
    private readonly ApiOptions _options = options.Value;

    public async Task SendAsync(ScaleReading reading, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/balanzas/{_options.ScaleId}/lecturas");
        request.Headers.Add("X-Agent-Key", apiKeyProvider.GetApiKey());
        request.Content = JsonContent.Create(new RegistrarLecturaRequest(
            reading.ReadingId,
            reading.WeightKg,
            reading.IsStable,
            reading.ReadAt));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed record RegistrarLecturaRequest(
    Guid LecturaId,
    decimal PesoKg,
    bool Estable,
    DateTimeOffset FechaLectura);
