namespace AnomalyDetection.Core.Models;

public sealed class EvidenceBundle
{
    public required AnomalyMessage Anomaly { get; init; }
    public IReadOnlyList<EvidenceDocument> Documents { get; init; } = [];
    public bool HasEvidence => Documents.Count > 0;
}
