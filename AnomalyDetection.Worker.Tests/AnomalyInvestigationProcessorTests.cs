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
                Evidence = ["db wait time increased"],
                Recommendations = ["scale the database"],
                AffectedServices = ["checkout"],
                Explanation = "Database latency aligned with anomaly timing"
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
