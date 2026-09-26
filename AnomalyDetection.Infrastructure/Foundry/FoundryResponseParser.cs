using System.Text.Json;
using AnomalyDetection.Core.Models;

namespace AnomalyDetection.Infrastructure.Foundry;

public static class FoundryResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParse(JsonElement root, out FoundryResponse response)
    {
        response = new FoundryResponse();
        var outputText = TryReadOutputText(root);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FoundryResponse>(outputText, SerializerOptions);
            if (parsed is null)
            {
                return false;
            }

            response = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryReadOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputTextElement) && outputTextElement.ValueKind == JsonValueKind.String)
        {
            return outputTextElement.GetString();
        }

        if (!root.TryGetProperty("output", out var outputArray) || outputArray.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var output in outputArray.EnumerateArray())
        {
            if (!output.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var content in contentArray.EnumerateArray())
            {
                if (content.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    return textElement.GetString();
                }
            }
        }

        return null;
    }
}
