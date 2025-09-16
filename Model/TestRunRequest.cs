using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestRunRequest()
{
         [JsonPropertyName("targetUrl")] public string? TargetUrl { get; init; }
         [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; init; }

         [JsonPropertyName("numCalls")] public int NumCalls { get; init; }
         [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; init; }
         
         [JsonPropertyName("multiItemPercentage")] public int MultiItemPercentage { get; init; } = 10; // percentage of calls that should use multi-item payloads
         [JsonPropertyName("maxConcurrency")] public int MaxConcurrency { get; init; } = 50;
         [JsonPropertyName("timeoutSeconds")] public int TimeoutSeconds { get; init; } = 100;
         
         
         public (bool IsValid, string? Error) Validate()
         {
             if (string.IsNullOrWhiteSpace(TargetUrl)) return (false, "targetBaseUrl is required");
             if (NumCalls <= 0) return (false, "numCalls must be > 0");
             if (DurationSeconds < 0) return (false, "durationSeconds must be >= 0");
             return (true, null);
         }

};