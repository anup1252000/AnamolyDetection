using System.ComponentModel.DataAnnotations;
using AnomalyDetection.Api.Controllers;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnomalyDetection.Api.Tests;

public sealed class AnomalyApiTests
{
    [Fact]
    public void RequestValidation_FailsForMissingRequiredFields()
    {
        var request = new AnomalyInvestigationRequest();
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);

        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public async Task Controller_ReturnsAcceptedAndPublishesMessage()
    {
        var publisher = new FakePublisher();
        var controller = new AnomalyController(publisher, NullLogger<AnomalyController>.Instance);
        var request = new AnomalyInvestigationRequest
        {
            JobId = "job-1",
            Detector = "high-latency",
            AnomalyScore = 91,
            Timestamp = DateTimeOffset.UtcNow,
            Service = "orders-api",
            Environment = "prod",
            ResultType = "record",
            Actual = [123],
            Typical = [80]
        };

        var result = await controller.InvestigateAsync(request, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(accepted.Value);
        Assert.NotNull(publisher.Message);
        Assert.Equal("job-1", publisher.Message!.JobId);
    }

    private sealed class FakePublisher : IAnomalyPublisher
    {
        public AnomalyMessage? Message { get; private set; }

        public Task PublishAsync(AnomalyMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }
}
