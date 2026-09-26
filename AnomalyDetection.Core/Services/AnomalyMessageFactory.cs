using System.Security.Cryptography;
using System.Text;
using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Core.Services;

public static class AnomalyMessageFactory
{
    public static AnomalyMessage Create(AnomalyInvestigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AnomalyMessage
        {
            Id = CreateId(request),
            JobId = request.JobId.Trim(),
            Detector = request.Detector.Trim(),
            AnomalyScore = request.AnomalyScore,
            Timestamp = request.Timestamp,
            Service = request.Service.Trim(),
            Environment = request.Environment.Trim(),
            Influencer = request.Influencer?.Trim(),
            ResultType = request.ResultType.Trim(),
            Actual = request.Actual,
            Typical = request.Typical,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    public static string CreateId(AnomalyInvestigationRequest request)
    {
        var value = string.Join('|',
            request.JobId.Trim(),
            request.Detector.Trim(),
            request.Timestamp.UtcDateTime.ToString("O"),
            request.Service.Trim(),
            request.Environment.Trim(),
            request.Influencer?.Trim() ?? string.Empty,
            request.ResultType.Trim());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }
}
