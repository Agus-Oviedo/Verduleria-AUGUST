using System.Collections.Concurrent;
using VerduleriaAugust.Api.DTOs;

namespace VerduleriaAugust.Api.Services;

public sealed class BalanzaLecturaStore
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VigenciaParaVenta = TimeSpan.FromMinutes(2);
    private readonly ConcurrentDictionary<int, LecturaBalanzaRespuestaDto> _porCaja = new();
    private readonly ConcurrentDictionary<Guid, LecturaBalanzaRespuestaDto> _porId = new();
    private readonly ConcurrentDictionary<Guid, byte> _reservadas = new();
    private readonly ConcurrentDictionary<Guid, byte> _utilizadas = new();

    public bool TrySet(
        int balanzaId,
        int cajaId,
        RegistrarLecturaBalanzaDto lectura,
        DateTimeOffset fechaRecepcion,
        out LecturaBalanzaRespuestaDto stored)
    {
        stored = new LecturaBalanzaRespuestaDto(
            lectura.LecturaId, balanzaId, cajaId, lectura.PesoKg,
            lectura.Estable, lectura.FechaLectura, fechaRecepcion, true);
        _porId[lectura.LecturaId] = stored;

        while (true)
        {
            if (!_porCaja.TryGetValue(cajaId, out var current))
                return _porCaja.TryAdd(cajaId, stored) ||
                       TrySet(balanzaId, cajaId, lectura, fechaRecepcion, out stored);

            if (current.LecturaId == lectura.LecturaId)
            {
                stored = current;
                return true;
            }

            if (current.FechaLectura >= lectura.FechaLectura)
                return false;

            if (_porCaja.TryUpdate(cajaId, stored, current))
                return true;
        }
    }

    public LecturaBalanzaRespuestaDto? GetCurrent(int cajaId, DateTimeOffset now)
    {
        if (!_porCaja.TryGetValue(cajaId, out var reading))
            return null;

        var vigente = now - reading.FechaRecepcion <= Vigencia;
        return reading with { Vigente = vigente };
    }

    public bool TryReserve(Guid lecturaId, int cajaId, DateTimeOffset now, out LecturaBalanzaRespuestaDto? reading)
    {
        reading = null;
        if (_utilizadas.ContainsKey(lecturaId) ||
            !_porId.TryGetValue(lecturaId, out var found) ||
            found.CajaId != cajaId || !found.Estable || found.PesoKg <= 0 ||
            now - found.FechaRecepcion > VigenciaParaVenta || !_reservadas.TryAdd(lecturaId, 0))
            return false;
        reading = found;
        return true;
    }

    public void Complete(Guid lecturaId)
    {
        _reservadas.TryRemove(lecturaId, out _);
        _utilizadas.TryAdd(lecturaId, 0);
    }

    public void Release(Guid lecturaId) => _reservadas.TryRemove(lecturaId, out _);
}
