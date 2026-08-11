using System.Text;

namespace VerduleriaAugust.ScaleAgent.Tests;

public class SystelFrameParserTests
{
    private readonly SystelFrameParser _parser = new();

    [Theory]
    [InlineData(" 0.710", 0.710)]
    [InlineData("12.345", 12.345)]
    [InlineData("-0.125", -0.125)]
    public void ParsesOfficialStableWeightFrame(string displayedWeight, decimal expected)
    {
        var payload = new List<byte> { 0x02 };
        payload.AddRange(Encoding.ASCII.GetBytes(displayedWeight));
        payload.Add(0x03);
        payload.Add(SystelFrameParser.CalculateXor([.. payload]));

        var parsed = _parser.TryParseStableWeight([.. payload], out var weight);

        Assert.True(parsed);
        Assert.Equal(expected, weight);
    }

    [Fact]
    public void RejectsFrameWithInvalidChecksum()
    {
        byte[] frame = [0x02, .. Encoding.ASCII.GetBytes(" 0.710"), 0x03, 0xFF];

        Assert.False(_parser.TryParseStableWeight(frame, out _));
    }

    [Fact]
    public void RejectsIncompleteFrame()
    {
        Assert.False(_parser.TryParseStableWeight([0x02, 0x30, 0x03], out _));
    }
}
