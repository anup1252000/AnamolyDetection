using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Core.Models;
using AnomalyDetection.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AnomalyDetection.Infrastructure.Foundry;

public sealed class FoundryService : IFoundryService
{
    private static readonly TokenRequestContext TokenRequestContext = new(["https://cognitiveservices.azure.com/.default"]);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly DefaultAzureCredential _credential;
    private readonly FoundryOptions _options;
    private readonly ILogger<FoundryService> _logger;

    public FoundryService(HttpClient httpClient, IOptions<FoundryOptions> options, ILogger<FoundryService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _credential = new DefaultAzureCredential();

        if (_httpClient.Timeout == Timeout.InfiniteTimeSpan)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
        }
    }

    public async Task<FoundryResponse> AnalyzeAsync(EvidenceBundle evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (!evidence.HasEvidence)
        {
            return CreateInsufficientEvidenceResponse();
        }

        _logger.LogInformation("Foundry call started for anomaly {AnomalyId}", evidence.Anomaly.Id);

        var accessToken = await _credential.GetTokenAsync(TokenRequestContext, cancellationToken);
        var requestBody = BuildRequestBody(evidence);
        var maxAttempts = Math.Max(1, _options.MaxRetries + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildRequestUri())
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    if (ShouldRetry(response.StatusCode) && attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "Foundry call returned status {StatusCode} for anomaly {AnomalyId} on attempt {Attempt}/{MaxAttempts}. Retrying.",
                            (int)response.StatusCode,
                            evidence.Anomaly.Id,
                            attempt,
                            maxAttempts);

                        await DelayBeforeRetryAsync(cancellationToken);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

                if (!FoundryResponseParser.TryParse(document.RootElement, out var foundryResponse))
                {
                    _logger.LogWarning("Foundry response parsing failed for anomaly {AnomalyId}", evidence.Anomaly.Id);
                    return CreateInsufficientEvidenceResponse();
                }

                _logger.LogInformation(
                    "Foundry call completed for anomaly {AnomalyId} on attempt {Attempt}/{MaxAttempts}",
                    evidence.Anomaly.Id,
                    attempt,
                    maxAttempts);

                return foundryResponse;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Transient Foundry HTTP error for anomaly {AnomalyId} on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    evidence.Anomaly.Id,
                    attempt,
                    maxAttempts);

                await DelayBeforeRetryAsync(cancellationToken);
            }
        }

        return CreateInsufficientEvidenceResponse();
    }

    private Uri BuildRequestUri()
    {
        var baseUri = _options.ProjectEndpoint.TrimEnd('/');
        return new Uri(
            $"{baseUri}/applications/{Uri.EscapeDataString(_options.AgentName)}/protocols/openai/responses?api-version={_options.ApiVersion}",
            UriKind.Absolute);
    }

    private static string BuildRequestBody(EvidenceBundle evidence)
    {
        var anomaly = evidence.Anomaly;
        var evidenceArray = new JsonArray();
        foreach (var document in evidence.Documents.Take(25))
        {
            evidenceArray.Add(new JsonObject
            {
                ["timestamp"] = document.Timestamp?.ToString("O"),
                ["service"] = document.Service,
                ["environment"] = document.Environment,
                ["route"] = document.Route,
                ["duration"] = document.Duration,
                ["statusCode"] = document.StatusCode,
                ["message"] = document.Message,
                ["exception"] = document.Exception,
                ["traceId"] = document.TraceId,
                ["additionalFields"] = JsonSerializer.SerializeToNode(document.AdditionalFields, SerializerOptions)
            });
        }

        var prompt = new StringBuilder()
            .AppendLine("You are an application production incident RCA agent.")
            .AppendLine("Analyze application anomalies using ONLY the supplied anomaly information and evidence.")
            .AppendLine("Never invent logs, metrics, traces, dependencies, deployments, configuration changes, infrastructure information, or behavior.")
            .AppendLine("Clearly distinguish observed facts, hypotheses, and confirmed root cause.")
            .AppendLine("Only identify a root cause when supported by evidence.")
            .AppendLine("If evidence is insufficient: severity = Unknown, confidence = 0, rootCause = 'Insufficient evidence'.")
            .AppendLine("Provide practical remediation recommendations.")
            .AppendLine("Return structured JSON only.")
            .AppendLine()
            .AppendLine($"Anomaly jobId: {anomaly.JobId}")
            .AppendLine($"Detector: {anomaly.Detector}")
            .AppendLine($"Anomaly score: {anomaly.AnomalyScore}")
            .AppendLine($"Timestamp: {anomaly.Timestamp:O}")
            .AppendLine($"Service: {anomaly.Service}")
            .AppendLine($"Environment: {anomaly.Environment}")
            .AppendLine($"Influencer: {anomaly.Influencer}")
            .AppendLine($"Result type: {anomaly.ResultType}")
            .AppendLine($"Actual: {string.Join(",", anomaly.Actual)}")
            .AppendLine($"Typical: {string.Join(",", anomaly.Typical)}")
            .AppendLine("Evidence documents are provided separately in JSON.")
            .ToString();

        var request = new JsonObject
        {
            ["input"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "input_text",
                            ["text"] = prompt
                        }
                    }
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "input_text",
                            ["text"] = evidenceArray.ToJsonString()
                        }
                    }
                }
            },
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = "anomaly_rca",
                    ["strict"] = true,
                    ["schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["properties"] = new JsonObject
                        {
                            ["severity"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JsonArray(SeverityLevels.Critical, SeverityLevels.High, SeverityLevels.Medium, SeverityLevels.Low, SeverityLevels.Unknown)
                            },
                            ["rootCause"] = new JsonObject { ["type"] = "string" },
                            ["confidence"] = new JsonObject { ["type"] = "number" },
                            ["observedFacts"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" }
                            },
                            ["hypotheses"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["additionalProperties"] = false,
                                    ["properties"] = new JsonObject
                                    {
                                        ["description"] = new JsonObject { ["type"] = "string" },
                                        ["confidence"] = new JsonObject { ["type"] = "number" },
                                        ["evidence"] = new JsonObject
                                        {
                                            ["type"] = "array",
                                            ["items"] = new JsonObject { ["type"] = "string" }
                                        }
                                    },
                                    ["required"] = new JsonArray("description", "confidence", "evidence")
                                }
                            },
                            ["affectedServices"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["additionalProperties"] = false,
                                    ["properties"] = new JsonObject
                                    {
                                        ["service"] = new JsonObject { ["type"] = "string" },
                                        ["environment"] = new JsonObject { ["type"] = "string" },
                                        ["notes"] = new JsonObject { ["type"] = "string" }
                                    },
                                    ["required"] = new JsonArray("service", "environment", "notes")
                                }
                            },
                            ["evidence_supporting_conclusion"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" }
                            },
                            ["recommendedRemediations"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" }
                            },
                            ["explanation"] = new JsonObject { ["type"] = "string" },
                            ["notes"] = new JsonObject { ["type"] = "string" }
                        },
                        ["required"] = new JsonArray(
                            "severity",
                            "rootCause",
                            "confidence",
                            "observedFacts",
                            "hypotheses",
                            "affectedServices",
                            "evidence_supporting_conclusion",
                            "recommendedRemediations",
                            "explanation",
                            "notes")
                    }
                }
            }
        };

        return request.ToJsonString();
    }

    private async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        if (_options.RetryDelaySeconds <= 0)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(_options.RetryDelaySeconds), cancellationToken);
    }

    private static bool ShouldRetry(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return statusCode == System.Net.HttpStatusCode.RequestTimeout ||
               statusCode == System.Net.HttpStatusCode.TooManyRequests ||
               code >= 500;
    }

    private static FoundryResponse CreateInsufficientEvidenceResponse()
    {
        return new FoundryResponse
        {
            Severity = SeverityLevels.Unknown,
            RootCause = "Insufficient evidence",
            Confidence = 0,
            ObservedFacts = [],
            Hypotheses = [],
            AffectedServices = [],
            EvidenceSupportingConclusion = [],
            RecommendedRemediations = [],
            Explanation = "Insufficient evidence",
            Notes = "Insufficient evidence"
        };
    }
}
