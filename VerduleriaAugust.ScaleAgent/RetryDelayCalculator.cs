namespace VerduleriaAugust.ScaleAgent;

public static class RetryDelayCalculator
{
    public static TimeSpan Calculate(
        int consecutiveFailures,
        int baseDelayMilliseconds,
        int maxDelaySeconds)
    {
        if (consecutiveFailures <= 0)
            return TimeSpan.FromMilliseconds(baseDelayMilliseconds);

        var exponent = Math.Min(consecutiveFailures - 1, 20);
        var milliseconds = baseDelayMilliseconds * Math.Pow(2, exponent);
        return TimeSpan.FromMilliseconds(Math.Min(
            milliseconds,
            TimeSpan.FromSeconds(maxDelaySeconds).TotalMilliseconds));
    }
}
