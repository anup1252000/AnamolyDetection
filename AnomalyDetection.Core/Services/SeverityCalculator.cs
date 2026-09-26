using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public static class SeverityCalculator
{
    public static string FromScore(double anomalyScore)
    {
        if (anomalyScore >= 90)
        {
            return SeverityLevels.Critical;
        }

        if (anomalyScore >= 75)
        {
            return SeverityLevels.High;
        }

        if (anomalyScore >= 50)
        {
            return SeverityLevels.Medium;
        }

        return SeverityLevels.Low;
    }
}
