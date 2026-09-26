# GitHub Copilot Instructions

## Goal

Build production-ready anomaly detection capabilities using:

- ASP.NET Core Web API
- C#
- Elasticsearch
- Kibana ML anomaly detection
- Azure AI Foundry
- RabbitMQ
- OpenTelemetry where applicable

Optimize for:
1. Correctness
2. Production reliability
3. Performance
4. Maintainability
5. Minimal unnecessary code and explanation

## Coding Rules

- Use modern C# and nullable reference types.
- Prefer async/await for I/O.
- Pass CancellationToken through all asynchronous application/service methods.
- Use dependency injection.
- Use Options pattern for configuration.
- Never hard-code URLs, index names, field names, credentials, queue names, or environment-specific values.
- Keep secrets out of source code and appsettings.json.
- Use environment variables, managed identity, workload identity, or secret stores.
- Use strongly typed configuration classes.
- Keep methods focused on one responsibility.
- Avoid unnecessary abstractions, wrappers, factories, or interfaces.
- Do not introduce a design pattern unless it solves a demonstrated problem.
- Prefer simple code over clever code.
- Do not generate boilerplate that is not required.

## Architecture

Use this flow:

Existing Elasticsearch
→ Kibana ML anomaly detection
→ ASP.NET Core anomaly endpoint
→ RabbitMQ
→ RCA worker
→ Elasticsearch evidence
→ Azure AI Foundry
→ ai-anomalies-* index
→ Kibana dashboard

Responsibilities:

Kibana:
- anomaly detection
- visualization
- dashboards
- alerts

ASP.NET Core:
- API
- configuration
- orchestration
- validation

RabbitMQ:
- asynchronous anomaly processing

Worker:
- retrieve evidence
- correlate telemetry
- call Foundry
- persist RCA

Azure AI Foundry:
- RCA
- correlation
- explanation
- recommendations

Elasticsearch:
- source telemetry
- anomaly evidence
- RCA results

Do not move anomaly detection into the LLM.

## Elasticsearch

- Use the existing Elasticsearch index as the telemetry source.
- Never assume `logs-*`.
- Index patterns must come from configuration.
- Timestamp, service, environment, duration, status, route, exception, message, and trace fields must be configurable.
- Do not modify production telemetry documents unnecessarily.
- Store AI results separately using `ai-anomalies-*`.
- Use explicit mappings for AI result indices.
- Query only the evidence window required for an anomaly.
- Avoid retrieving entire indices.
- Avoid unbounded Elasticsearch queries.
- Always define reasonable `size`, filters, and time ranges.
- Preserve Elasticsearch field names through configuration.
- Support nested/dotted fields where applicable.

## Kibana

- Create/manage Data Views through Kibana APIs.
- Create/manage anomaly detection jobs through Elasticsearch ML APIs.
- Create/manage datafeeds through Elasticsearch ML APIs.
- Do not write directly to `.kibana`.
- Do not hard-code Kibana saved-object IDs unless explicitly required.
- Keep dashboard configuration separate from application code.
- Dashboard filters should support time, service, environment, detector, and severity.

## Anomaly Detection

Detectors must be configurable.

Possible detectors:

- high latency
- high error rate
- exception increase
- request throughput decrease
- dependency latency increase

Bucket span must be configurable.

Influencers must be configurable.

Do not automatically add detectors that are not requested.

Use deterministic rules for severity.

Example:

score >= 90 → Critical
score >= 75 → High
score >= 50 → Medium
otherwise → Low

Do not ask the LLM to calculate anomaly scores.

## Azure AI Foundry

Use Azure AI Foundry only after an anomaly has been detected.

Send structured evidence rather than raw unlimited logs.

Evidence can include:

- anomaly score
- detector
- service
- environment
- latency
- status codes
- exceptions
- database evidence
- messaging evidence
- deployment information
- trace information

The AI must:

- use only supplied evidence
- distinguish facts from hypotheses
- avoid invented metrics
- provide confidence
- provide root cause
- provide evidence
- provide recommendations

If evidence is insufficient:

severity = Unknown
confidence = 0
rootCause = Insufficient evidence

Prefer structured JSON responses.

## RabbitMQ

Use RabbitMQ for asynchronous RCA processing.

Do not block the HTTP request while waiting for Azure AI Foundry.

API should acknowledge anomaly submission quickly.

Worker should:
1. consume anomaly
2. retrieve evidence
3. call Foundry
4. persist RCA
5. acknowledge message

Handle:
- retry
- transient failures
- dead-lettering
- idempotency

Do not introduce MassTransit unless explicitly requested.

Prefer RabbitMQ.Client when implementing RabbitMQ directly.

## Performance

Performance is important.

- Avoid unnecessary allocations.
- Avoid loading large Elasticsearch result sets into memory.
- Limit evidence size.
- Use streaming where appropriate.
- Use async I/O.
- Do not perform sequential external calls when they can safely execute concurrently.
- Do not introduce parallelism blindly.
- Respect Elasticsearch and RabbitMQ limits.
- Avoid excessive logging.
- Do not serialize the same large object multiple times.
- Do not send unnecessary telemetry to Foundry.

## Resilience

External dependencies include:

- Elasticsearch
- Kibana
- RabbitMQ
- Azure AI Foundry

Use appropriate timeout, retry, and failure handling.

Do not retry permanent errors.

Do not create retry loops that can amplify production incidents.

## Logging

Use structured logging.

Prefer:

_logger.LogInformation(
    "Anomaly investigation started for {Service} with score {Score}",
    service,
    score);

Avoid:

_logger.LogInformation(
    $"Anomaly investigation started for {service}");

Never log:

- API keys
- passwords
- tokens
- secrets
- sensitive payloads

Do not log entire Elasticsearch documents unless explicitly required.

## Error Handling

Use meaningful exceptions and HTTP status codes.

API:

400 → invalid request
404 → required resource not found
409 → duplicate/conflicting operation
429 → throttling
500 → unexpected server error
503 → dependency unavailable

Do not catch `Exception` unless there is a meaningful recovery or boundary-level handling.

Do not silently swallow exceptions.

## API

Use RESTful endpoints.

Example:

POST /api/anomaly/investigate

GET /health

Use request/response DTOs.

Do not expose infrastructure models directly from API contracts.

Validate incoming requests.

Do not duplicate validation across controller and service unless necessary.

## Testing

Tests must cover:

- anomaly request validation
- severity calculation
- Elasticsearch evidence mapping
- Foundry response parsing
- RCA orchestration
- RabbitMQ message processing
- failure/retry behavior

Prefer xUnit.

Use mocks/fakes only where they provide isolation.

Do not create tests that simply verify framework behavior.

Tests should verify business behavior.

## Code Generation

When modifying existing code:

- inspect the existing implementation first
- preserve existing behavior unless the request requires a change
- make the smallest safe change
- do not rewrite unrelated files
- do not rename public APIs unnecessarily
- do not introduce new packages unless required

When creating code:

- generate only files required for the requested feature
- reuse existing utilities and infrastructure
- follow existing project conventions
- do not create duplicate services

## Dependencies

Before adding a NuGet package:

1. Check whether the existing framework already provides the capability.
2. Check whether an existing project dependency provides it.
3. Add a package only when necessary.

Do not introduce libraries for trivial functionality.

## Configuration

All environment-specific values must be configurable.

Use:

IOptions<T>

for application configuration.

Use environment variables/secrets for:

- Elasticsearch API keys
- Kibana API keys
- Azure credentials
- RabbitMQ credentials

Never commit secrets.

## Output Discipline

When generating code:

- provide complete compilable code when requested
- do not generate pseudocode unless explicitly requested
- do not omit important error handling
- do not remove required production behavior merely to shorten the response
- do not repeat architecture explanations already present in the repository
- keep explanations concise
- explain only non-obvious decisions

When asked to modify code, return the changed code and a short summary.

Do not regenerate unchanged files.

## Repository Context

Before implementing a feature:

1. Inspect existing project structure.
2. Search for existing implementations.
3. Reuse existing services/configuration/logging patterns.
4. Follow existing naming conventions.
5. Modify the minimum number of files.

Do not create a parallel implementation when an existing implementation can be extended.

## Priority

When instructions conflict, prioritize:

1. Correctness
2. Existing repository conventions
3. Security
4. Production reliability
5. Performance
6. Maintainability
7. Minimal code/token usage