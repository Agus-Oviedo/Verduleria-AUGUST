using System.Globalization;
using System.Text;

namespace VerduleriaAugust.ScaleAgent;

public sealed class SystelFrameParser
{
    public const byte RequestStableWeight = 0x05;
    public const byte UnstableWeight = 0x11;
    private const byte StartOfText = 0x02;
    private const byte EndOfText = 0x03;

    public bool TryParseStableWeight(ReadOnlySpan<byte> frame, out decimal weightKg)
    {
        weightKg = 0;

        if (frame.Length is not (9 or 10) ||
            frame[0] != StartOfText ||
            frame[^2] != EndOfText ||
            CalculateXor(frame[..^1]) != frame[^1])
        {
            return false;
        }

        var weightText = Encoding.ASCII.GetString(frame[1..^2]).Trim();
        return decimal.TryParse(
            weightText,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out weightKg);
    }

    public static byte CalculateXor(ReadOnlySpan<byte> bytes)
    {
        byte result = 0;
        foreach (var value in bytes)
            result ^= value;
        return result;
    }
}
