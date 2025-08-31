using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestItemDiscrepancy() 
{
    [JsonPropertyName("requestId")] public string? RequestId {get; set;}
    [JsonPropertyName("callId")] public string? CallId {get; set;}
    [JsonPropertyName("callNumber")] public int CallNumber {get; set;}
    [JsonPropertyName("depositTransactionId")] public string? DepositTransactionId {get; set;}
    [JsonPropertyName("symxOutboundId")] public string? SymxOutboundId {get; set;}
    [JsonPropertyName("symxSyntheticRequestId")] public string? SymxSyntheticRequestId {get; set;}
    [JsonPropertyName("expectedValue")] public decimal ExpectedValue {get; set;}
    [JsonPropertyName("actualValue")] public decimal ActualValue {get; set;}
    [JsonPropertyName("discrepancyType")] public string? DiscrepancyType {get; set;} // e.g., "Missing", "Mismatch"
}