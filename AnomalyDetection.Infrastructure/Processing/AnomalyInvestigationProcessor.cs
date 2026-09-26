using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;

namespace AnomalyDetection.Infrastructure.Processing;

public sealed class AnomalyInvestigationProcessor : IAnomalyInvestigationProcessor
{
    private readonly IElasticsearchEvidenceService _evidenceService;
    private readonly IFoundryService _foundryService;
    private readonly IRcaPersistenceService _persistenceService;
    private readonly ILogger<AnomalyInvestigationProcessor> _logger;

    public AnomalyInvestigationProcessor(
        IElasticsearchEvidenceService evidenceService,
        IFoundryService foundryService,
        IRcaPersistenceService persistenceService,
        ILogger<AnomalyInvestigationProcessor> logger)
    {
        _evidenceService = evidenceService;
        _foundryService = foundryService;
        _persistenceService = persistenceService;
        _logger = logger;
    }

    public async Task ProcessAsync(AnomalyMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (await _persistenceService.ExistsAsync(message.Id, cancellationToken))
        {
            _logger.LogInformation("Skipping already-processed anomaly {AnomalyId}", message.Id);
            return;
        }

        _logger.LogInformation("Investigation started for anomaly {AnomalyId}", message.Id);

        await _persistenceService.EnsureIndexAsync(cancellationToken);

        var evidence = await _evidenceService.GetEvidenceAsync(message, cancellationToken);
        var rca = await _foundryService.AnalyzeAsync(evidence, cancellationToken);
        var severity = evidence.HasEvidence ? NormalizeSeverity(rca.Severity, message.AnomalyScore) : SeverityLevels.Unknown;

        await _persistenceService.PersistAsync(new RcaRecord
        {
            Id = message.Id,
            Timestamp = message.Timestamp,
            JobId = message.JobId,
            Detector = message.Detector,
            AnomalyScore = message.AnomalyScore,
            Service = message.Service,
            Environment = message.Environment,
            Influencer = message.Influencer,
            ResultType = message.ResultType,
            Actual = message.Actual,
            Typical = message.Typical,
            Severity = severity,
            RootCause = evidence.HasEvidence ? rca.RootCause : "Insufficient evidence",
            Confidence = evidence.HasEvidence ? rca.Confidence : 0,
            Explanation = evidence.HasEvidence ? rca.Explanation : "Insufficient evidence",
            Notes = evidence.HasEvidence ? rca.Notes : "Insufficient evidence",
            ObservedFacts = evidence.HasEvidence ? rca.ObservedFacts : [],
            Hypotheses = evidence.HasEvidence ? rca.Hypotheses : [],
            AffectedServices = evidence.HasEvidence ? rca.AffectedServices : [],
            EvidenceSupportingConclusion = evidence.HasEvidence ? rca.EvidenceSupportingConclusion : [],
            RecommendedRemediations = evidence.HasEvidence ? rca.RecommendedRemediations : []
        }, cancellationToken);

        _logger.LogInformation("RCA persisted for anomaly {AnomalyId}", message.Id);
    }

    private static string NormalizeSeverity(string? foundrySeverity, double anomalyScore)
    {
        if (string.Equals(foundrySeverity, SeverityLevels.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return SeverityLevels.Unknown;
        }

        return SeverityCalculator.FromScore(anomalyScore);
    }
}
