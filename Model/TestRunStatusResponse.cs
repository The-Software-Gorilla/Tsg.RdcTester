using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestRunStatusResponse()
{
    
    [JsonPropertyName("totalCalls")] public int TotalCalls {get; set;}
    [JsonPropertyName("completedCalls")] public int CompletedCalls {get; set;}
    [JsonPropertyName("status")] public string? Status {get; set;}
    [JsonPropertyName("startedUtc")] public DateTimeOffset? StartedUtc {get; set;}
    [JsonPropertyName("lastUpdatedUtc")] public DateTimeOffset? LastUpdatedUtc {get; set;}
    [JsonPropertyName("duration")] public int Duration {get; set;} // duration in seconds

}