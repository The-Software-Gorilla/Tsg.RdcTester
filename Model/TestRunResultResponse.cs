using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestRunResultResponse()
{
    [JsonPropertyName("testRunStatus")] public TestRunStatusResponse? TestRunStatus {get; set;}
    [JsonPropertyName("maxDoDepositRequestInvokeTimeMs")] public int MaxDoDepositRequestInvokeTimeMs {get; set;}
    [JsonPropertyName("minDoDepositRequestInvokeTimeMs")] public int MinDoDepositRequestInvokeTimeMs {get; set;}
    [JsonPropertyName("avgDoDepositRequestInvokeTimeMs")] public int AvgDoDepositRequestInvokeTimeMs {get; set;}
    [JsonPropertyName("maxDoDepositRequestDurationMs")] public int MaxDoDepositRequestDurationMs {get; set;}
    [JsonPropertyName("minDoDepositRequestDurationMs")] public int MinDoDepositRequestDurationMs {get; set;}
    [JsonPropertyName("avgDoDepositRequestDurationMs")] public int AvgDoDepositRequestDurationMs {get; set;}
    [JsonPropertyName("maxMultiItemInvokeTimeMs")] public int MaxMultiItemInvokeTimeMs {get; set;}
    [JsonPropertyName("minMultiItemInvokeTimeMs")] public int MinMultiItemInvokeTimeMs {get; set;}
    [JsonPropertyName("avgMultiItemInvokeTimeMs")] public int AvgMultiItemInvokeTimeMs {get; set;}
    [JsonPropertyName("maxMultiDepositDurationMs")] public int MaxMultiDepositDurationMs {get; set;}
    [JsonPropertyName("minMultiDepositDurationMs")] public int MinMultiDepositDurationMs {get; set;}
    [JsonPropertyName("avgMultiDepositDurationMs")] public int AvgMultiDepositDurationMs {get; set;}
    [JsonPropertyName("maxSingleItemInvokeTimeMs")] public int MaxSingleItemInvokeTimeMs {get; set;}
    [JsonPropertyName("minSingleItemInvokeTimeMs")] public int MinSingleItemInvokeTimeMs {get; set;}
    [JsonPropertyName("avgSingleItemInvokeTimeMs")] public int AvgSingleItemInvokeTimeMs {get; set;}
    [JsonPropertyName("maxSingleDepositDurationMs")] public int MaxSingleDepositDurationMs {get; set;}
    [JsonPropertyName("minSingleDepositDurationMs")] public int MinSingleDepositDurationMs {get; set;}
    [JsonPropertyName("avgSingleDepositDurationMs")] public int AvgSingleDepositDurationMs {get; set;}
    [JsonPropertyName("maxSymxCallDurationMs")] public int MaxSymxCallDurationMs {get; set;}
    [JsonPropertyName("minSymxCallDurationMs")] public int MinSymxCallDurationMs {get; set;}
    [JsonPropertyName("avgSymxCallDurationMs")] public int AvgSymxCallDurationMs {get; set;}
    [JsonPropertyName("singleItemCount")] public int SingleItemCount { get; set; }
    [JsonPropertyName("multiItemCount")] public int MultiItemCount { get; set; }
    [JsonPropertyName("successfulDeposits")] public int SuccessfulDeposits { get; set; }
    [JsonPropertyName("failedDeposits")] public int FailedDeposits { get; set; }
    [JsonPropertyName("successfulCalls")] public int SuccessfulSymxCalls { get; set; }
    [JsonPropertyName("failedCalls")] public int FailedSymxCalls { get; set; }
    [JsonPropertyName("depositItemDiscrepancyCount")] public int DepositItemDiscrepancyCount { get; set; }
    [JsonPropertyName("depositItemDiscrepancies")] public List<TestItemDiscrepancy>? DepositItemDiscrepancies { get; set; }
    
}