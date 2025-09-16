using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestRunResultRequest()
{
    [JsonPropertyName("maxInvokeTimeMs")] public int MaxInvokeTimeMs { get; init; } = 3000; // max time to a single call should take to complete
    [JsonPropertyName("invokePercentageThreshold")] public double InvokePercentageThreshold { get; init; } = 0.5; // percentage of anomalies to total calls that is acceptable
    [JsonPropertyName("maxDepositTimeMs")] public int MaxDepositTimeMs { get; init; } = 3000; // max time to a single call should take to complete
    [JsonPropertyName("depositPercentageThreshold")] public double DepositPercentageThreshold { get; init; } = 0.5; // percentage of anomalies to total calls that is acceptable
    [JsonPropertyName("maxSymxCallTimeMs")] public int MaxSymxCallTimeMs { get; init; } = 1000; // max time a symx call should take to complete
    [JsonPropertyName("symxPercentageThreshold")] public double SymxPercentageThreshold { get; init; } = 0.1; // percentage of anomalies to total calls that is acceptable
};