using System.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent.Tests;

public class ScaleAgentTests
{
    [Fact]
    public async Task Simulator_UsesConfiguredWeightAndStability()
    {
        await using var reader = new SimulatedScaleReader(Options.Create(new ScaleOptions
        {
            SimulatorWeightKg = 2.375m,
            SimulatorStable = false
        }));

        var reading = await reader.ReadAsync(CancellationToken.None);

        Assert.NotNull(reading);
        Assert.NotEqual(Guid.Empty, reading.ReadingId);
        Assert.Equal(2.375m, reading.WeightKg);
        Assert.False(reading.IsStable);
    }

    [Fact]
    public async Task ApiClient_SendsExpectedRouteHeaderAndBody()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        var apiOptions = Options.Create(new ApiOptions
        {
            BaseUrl = "https://api.test",
            ScaleId = 7,
            ApiKey = "clave-prueba"
        });
        var client = new ScaleApiClient(
            httpClient, apiOptions, new TestApiKeyProvider("clave-prueba"));
        var reading = new ScaleReading(
            Guid.NewGuid(), 1.250m, true, DateTimeOffset.UtcNow, "SIM:1.250");

        await client.SendAsync(reading, CancellationToken.None);

        Assert.Equal("https://api.test/api/balanzas/7/lecturas", handler.Uri!.ToString());
        Assert.Equal("clave-prueba", handler.ApiKey);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal(reading.ReadingId, json.RootElement.GetProperty("lecturaId").GetGuid());
        Assert.Equal(1.250m, json.RootElement.GetProperty("pesoKg").GetDecimal());
        Assert.True(json.RootElement.GetProperty("estable").GetBoolean());
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(1, 0.5)]
    [InlineData(2, 1.0)]
    [InlineData(3, 2.0)]
    [InlineData(10, 30.0)]
    public void RetryDelay_GrowsExponentiallyAndRespectsMaximum(
        int failures,
        double expectedSeconds)
    {
        var delay = RetryDelayCalculator.Calculate(failures, 500, 30);

        Assert.Equal(expectedSeconds, delay.TotalSeconds);
    }

    [Fact]
    public void DpapiProvider_DecryptsMachineProtectedApiKey()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.GetTempFileName();
        try
        {
            const string apiKey = "clave-protegida-prueba";
            var entropy = Encoding.UTF8.GetBytes("VerduleriaAugust.ScaleAgent.ApiKey.v1");
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(apiKey), entropy, DataProtectionScope.LocalMachine);
            File.WriteAllText(path, Convert.ToBase64String(protectedBytes));
            var provider = new DpapiApiKeyProvider(Options.Create(new ApiOptions
            {
                BaseUrl = "https://api.test",
                ScaleId = 1,
                ApiKeyProtectedFile = path
            }));

            Assert.Equal(apiKey, provider.GetApiKey());
            Assert.Equal(apiKey, provider.GetApiKey());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-Agent-Key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    private sealed class TestApiKeyProvider(string apiKey) : IApiKeyProvider
    {
        public string GetApiKey() => apiKey;
    }
}
