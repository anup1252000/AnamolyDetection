using System.Net;
using System.Net.Http;
using System.Text;
using AnomalyDetection.Core.Configuration;
using AnomalyDetection.Infrastructure.Kibana;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnomalyDetection.Infrastructure.Tests;

public sealed class KibanaBootstrapServiceTests
{
    [Fact]
    public async Task BootstrapAsync_LogsMlLicenseError_WhenMlLicenseIsUnavailable()
    {
        var handler = new SequenceHttpMessageHandler(new[]
        {
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.OK),
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":{\"reason\":\"current license is non-compliant for [ml]\"}}", Encoding.UTF8, "application/json")
            }
        });

        var factory = new TestHttpClientFactory(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5601")
        }, new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9200")
        });

        var service = new KibanaBootstrapService(
            factory,
            Options.Create(new KibanaOptions { Url = "http://localhost:5601", DataViewName = "Telemetry", ResultsDataViewName = "AI Anomalies", DashboardTitle = "Anomaly RCA Dashboard" }),
            Options.Create(new ElasticsearchOptions { Url = "http://localhost:9200", SourceIndex = "telemetry-*", AnomalyResultsIndex = "ai-anomalies-*" }),
            Options.Create(new AnomalyDetectionOptions { JobId = "job-1", DatafeedId = "datafeed-1", Detectors = [new DetectorOptions { Description = "Request volume anomaly", Function = "high_count" }] }),
            Options.Create(new SourceFieldsOptions()),
            NullLogger<KibanaBootstrapService>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.BootstrapAsync(CancellationToken.None));
    }

    private sealed class TestHttpClientFactory(HttpClient kibanaClient, HttpClient elasticClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return name.EndsWith("-kibana", StringComparison.Ordinal) ? kibanaClient : elasticClient;
        }
    }

    private sealed class SequenceHttpMessageHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
