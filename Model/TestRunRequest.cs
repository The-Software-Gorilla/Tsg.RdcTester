using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestRunRequest()
{
         [JsonPropertyName("targetBaseUrl")] public string? TargetBaseUrl { get; init; }
         [JsonPropertyName("endpoint")] public string? Endpoint { get; init; }
         [JsonPropertyName("httpMethod")] public string? HttpMethod { get; init; } = "POST"; // POST, GET, PUT, DELETE
         [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; init; }

         [JsonPropertyName("numCalls")] public int NumCalls { get; init; }
         [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; init; }
         [JsonPropertyName("maxConcurrency")] public int MaxConcurrency { get; init; } = 50;
         [JsonPropertyName("timeoutSeconds")] public int TimeoutSeconds { get; init; } = 100;

         [JsonPropertyName("pools")] public Dictionary<string, JsonElement>? Pools { get; init; } // named lists for random selection

         public (bool IsValid, string? Error) Validate()
         {
             if (string.IsNullOrWhiteSpace(TargetBaseUrl)) return (false, "targetBaseUrl is required");
             if (string.IsNullOrWhiteSpace(Endpoint)) return (false, "endpoint is required");
             if (NumCalls <= 0) return (false, "numCalls must be > 0");
             if (DurationSeconds < 0) return (false, "durationSeconds must be >= 0");
             return (true, null);
         }

};