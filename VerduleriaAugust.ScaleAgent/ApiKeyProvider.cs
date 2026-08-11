using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent;

public interface IApiKeyProvider
{
    string GetApiKey();
}

public sealed class DpapiApiKeyProvider(IOptions<ApiOptions> options) : IApiKeyProvider
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
        "VerduleriaAugust.ScaleAgent.ApiKey.v1");
    private readonly ApiOptions _options = options.Value;
    private string? _cachedApiKey;

    public string GetApiKey()
    {
        if (_cachedApiKey != null)
            return _cachedApiKey;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            return _cachedApiKey = _options.ApiKey;

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "El archivo de clave DPAPI solamente puede abrirse en Windows.");

        var path = Path.GetFullPath(_options.ApiKeyProtectedFile!);
        var protectedBytes = Convert.FromBase64String(File.ReadAllText(path).Trim());
        var clearBytes = ProtectedData.Unprotect(
            protectedBytes,
            Entropy,
            DataProtectionScope.LocalMachine);
        try
        {
            _cachedApiKey = Encoding.UTF8.GetString(clearBytes);
            if (string.IsNullOrWhiteSpace(_cachedApiKey))
                throw new InvalidOperationException("El archivo protegido no contiene una clave válida.");
            return _cachedApiKey;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }
}
