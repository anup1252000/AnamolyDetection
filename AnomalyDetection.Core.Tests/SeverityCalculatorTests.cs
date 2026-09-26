using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;

namespace AnomalyDetection.Core.Tests;

public sealed class SeverityCalculatorTests
{
    [Theory]
    [InlineData(95, SeverityLevels.Critical)]
    [InlineData(80, SeverityLevels.High)]
    [InlineData(50, SeverityLevels.Medium)]
    [InlineData(49.9, SeverityLevels.Low)]
    public void FromScore_ReturnsExpectedSeverity(double score, string expected)
    {
        var severity = SeverityCalculator.FromScore(score);

        Assert.Equal(expected, severity);
    }
}
