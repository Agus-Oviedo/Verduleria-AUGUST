using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace VerduleriaAugust.ScaleAgent;

public sealed class SystelSerialScaleReader : IScaleReader
{
    private readonly ScaleOptions _options;
    private readonly SystelFrameParser _parser;
    private readonly ILogger<SystelSerialScaleReader> _logger;
    private SerialPort? _port;

    public SystelSerialScaleReader(
        IOptions<ScaleOptions> options,
        SystelFrameParser parser,
        ILogger<SystelSerialScaleReader> logger)
    {
        _options = options.Value;
        _parser = parser;
        _logger = logger;
    }

    public async Task<ScaleReading?> ReadAsync(CancellationToken cancellationToken)
    {
        EnsureOpen();
        var port = _port!;
        port.DiscardInBuffer();
        port.Write([SystelFrameParser.RequestStableWeight], 0, 1);

        var frame = new List<byte>(10);
        var deadline = DateTime.UtcNow.AddMilliseconds(_options.ResponseTimeoutMilliseconds);
        while (DateTime.UtcNow < deadline && frame.Count < 10)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (port.BytesToRead == 0)
            {
                await Task.Delay(_options.InterByteTimeoutMilliseconds, cancellationToken);
                if (frame.Count > 0)
                    break;
                continue;
            }

            var value = (byte)port.ReadByte();
            if (frame.Count == 0 && value == SystelFrameParser.UnstableWeight)
                return new ScaleReading(Guid.NewGuid(), 0, false, DateTimeOffset.UtcNow, "11");
            frame.Add(value);
        }

        var raw = Convert.ToHexString([.. frame]);
        if (_parser.TryParseStableWeight(CollectionsMarshal.AsSpan(frame), out var weight))
            return new ScaleReading(Guid.NewGuid(), weight, true, DateTimeOffset.UtcNow, raw);

        if (frame.Count > 0)
            _logger.LogWarning("La balanza devolvió una trama inválida: {RawData}", raw);
        return null;
    }

    private void EnsureOpen()
    {
        if (_port?.IsOpen == true)
            return;

        _port?.Dispose();
        _port = new SerialPort(_options.PortName, _options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = _options.ResponseTimeoutMilliseconds,
            WriteTimeout = _options.ResponseTimeoutMilliseconds
        };
        _port.Open();
        _logger.LogInformation("Puerto serial {PortName} abierto a {BaudRate} baudios.",
            _options.PortName, _options.BaudRate);
    }

    public ValueTask DisposeAsync()
    {
        _port?.Dispose();
        return ValueTask.CompletedTask;
    }
}
