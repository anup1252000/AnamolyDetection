using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnomalyDetection.Api.Controllers;

[ApiController]
[Route("api/anomaly")]
public sealed class AnomalyController : ControllerBase
{
    private readonly IAnomalyPublisher _publisher;
    private readonly ILogger<AnomalyController> _logger;

    public AnomalyController(IAnomalyPublisher publisher, ILogger<AnomalyController> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    [HttpPost("investigate")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InvestigateAsync([FromBody] AnomalyInvestigationRequest request, CancellationToken cancellationToken)
    {
        if (request.Timestamp == default)
        {
            ModelState.AddModelError(nameof(request.Timestamp), "Timestamp is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var message = AnomalyMessageFactory.Create(request);

        _logger.LogInformation(
            "Anomaly received for service {Service} with score {Score} and detector {Detector}",
            message.Service,
            message.AnomalyScore,
            message.Detector);

        await _publisher.PublishAsync(message, cancellationToken);

        return Accepted(new
        {
            operationId = message.Id,
            status = "queued"
        });
    }
}
