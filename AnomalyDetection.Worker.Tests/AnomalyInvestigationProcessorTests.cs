using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using AnomalyDetection.Infrastructure.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnomalyDetection.Worker.Tests;

public sealed class AnomalyInvestigationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_SkipsProcessingWhenRecordAlreadyExists()
    {
        var persistence = new FakePersistenceService(exists: true);
        var processor = new AnomalyInvestigationProcessor(
            new FakeEvidenceService(),
            new FakeFoundryService(),
            persistence,
            NullLogger<AnomalyInvestigationProcessor>.Instance);

        await processor.ProcessAsync(CreateMessage(), CancellationToken.None);

        Assert.False(persistence.PersistCalled);
    }

    [Fact]
    public async Task ProcessAsync_PersistsUnknownSeverityWhenEvidenceIsMissing()
    {
        var persistence = new FakePersistenceService(exists: false);
        var processor = new AnomalyInvestigationProcessor(
            new FakeEvidenceService { Bundle = new EvidenceBundle { Anomaly = CreateMessage(), Documents = [] } },
            new FakeFoundryService(),
            persistence,
            NullLogger<AnomalyInvestigationProcessor>.Instance);

        await processor.ProcessAsync(CreateMessage(), CancellationToken.None);

        Assert.NotNull(persistence.Record);
        Assert.Equal(SeverityLevels.Unknown, persistence.Record!.Severity);
        Assert.Equal("Insufficient evidence", persistence.Record.RootCause);
        Assert.Empty(persistence.Record.ObservedFacts);
        Assert.Empty(persistence.Record.Hypotheses);
        Assert.Empty(persistence.Record.AffectedServices);
        Assert.Empty(persistence.Record.EvidenceSupportingConclusion);
        Assert.Empty(persistence.Record.RecommendedRemediations);
    }

    [Fact]
    public async Task ProcessAsync_PersistsExpandedFoundryResponseFields()
    {
        var persistence = new FakePersistenceService(exists: false);
        var processor = new AnomalyInvestigationProcessor(
            new FakeEvidenceService(),
            new FakeFoundryService(),
            persistence,
            NullLogger<AnomalyInvestigationProcessor>.Instance);

        await processor.ProcessAsync(CreateMessage(), CancellationToken.None);

        Assert.NotNull(persistence.Record);
        Assert.Single(persistence.Record!.ObservedFacts);
        Assert.Single(persistence.Record.Hypotheses);
        Assert.Single(persistence.Record.AffectedServices);
        Assert.Single(persistence.Record.EvidenceSupportingConclusion);
        Assert.Single(persistence.Record.RecommendedRemediations);
        Assert.Equal("Tune pool size", persistence.Record.Notes);
    }

    [Fact]
    public async Task ProcessAsync_PassesCancellationTokenThroughDependencies()
    {
        var persistence = new FakePersistenceService(exists: false);
        var evidence = new FakeEvidenceService();
        var foundry = new FakeFoundryService();
        var processor = new AnomalyInvestigationProcessor(
            evidence,
            foundry,
            persistence,
            NullLogger<AnomalyInvestigationProcessor>.Instance);

        using var cts = new CancellationTokenSource();

        await processor.ProcessAsync(CreateMessage(), cts.Token);

        Assert.Equal(cts.Token, evidence.Token);
        Assert.Equal(cts.Token, foundry.Token);
        Assert.Equal(cts.Token, persistence.ExistsToken);
        Assert.Equal(cts.Token, persistence.EnsureIndexToken);
        Assert.Equal(cts.Token, persistence.PersistToken);
    }

    private static AnomalyMessage CreateMessage()
    {
        return new AnomalyMessage
        {
            Id = "message-1",
            JobId = "job-1",
            Detector = "high-latency",
            AnomalyScore = 92,
            Timestamp = DateTimeOffset.UtcNow,
            Service = "checkout",
            Environment = "prod",
            ResultType = "record",
            Actual = [100],
            Typical = [50]
        };
    }

    private sealed class FakeEvidenceService : IElasticsearchEvidenceService
    {
        public EvidenceBundle? Bundle { get; init; }
        public CancellationToken Token { get; private set; }

        public Task<EvidenceBundle> GetEvidenceAsync(AnomalyMessage anomaly, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return Task.FromResult(Bundle ?? new EvidenceBundle
            {
                Anomaly = anomaly,
                Documents = [new EvidenceDocument { Service = anomaly.Service, Environment = anomaly.Environment }]
            });
        }
    }

    private sealed class FakeFoundryService : IFoundryService
    {
        public CancellationToken Token { get; private set; }

        public Task<FoundryResponse> AnalyzeAsync(EvidenceBundle evidence, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return Task.FromResult(new FoundryResponse
            {
                Severity = SeverityLevels.High,
                RootCause = "Database latency",
                Confidence = 0.9,
                ObservedFacts = ["db wait time increased"],
                Hypotheses =
                [
                    new FoundryHypothesis
                    {
                        Description = "Connection pool saturation",
                        Confidence = 0.7,
                        Evidence = ["timeouts increased"]
                    }
                ],
                AffectedServices =
                [
                    new FoundryAffectedService
                    {
                        Service = "checkout",
                        Environment = "prod",
                        Notes = "primary impact"
                    }
                ],
                EvidenceSupportingConclusion = ["db cpu saturation"],
                RecommendedRemediations = ["scale the database"],
                Explanation = "Database latency aligned with anomaly timing",
                Notes = "Tune pool size"
            });
        }
    }

    private sealed class FakePersistenceService(bool exists) : IRcaPersistenceService
    {
        private readonly bool _exists = exists;

        public bool PersistCalled { get; private set; }
        public RcaRecord? Record { get; private set; }
        public CancellationToken ExistsToken { get; private set; }
        public CancellationToken EnsureIndexToken { get; private set; }
        public CancellationToken PersistToken { get; private set; }

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            ExistsToken = cancellationToken;
            return Task.FromResult(_exists);
        }

        public Task EnsureIndexAsync(CancellationToken cancellationToken)
        {
            EnsureIndexToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task PersistAsync(RcaRecord record, CancellationToken cancellationToken)
        {
            PersistToken = cancellationToken;
            PersistCalled = true;
            Record = record;
            return Task.CompletedTask;
        }
    }
}
