using System.Text.Json.Serialization;

namespace Tsg.RdcTester.Model;

public record TestItemAnomaly() 
{
    [JsonPropertyName("requestId")] public string? RequestId {get; set;}
    [JsonPropertyName("callId")] public string? CallId {get; set;}
    [JsonPropertyName("callNumber")] public int CallNumber {get; set;}
    [JsonPropertyName("depositTransactionId")] public string? DepositTransactionId {get; set;}
    [JsonPropertyName("symxOutboundId")] public string? SymxOutboundId {get; set;}
    [JsonPropertyName("isMultiItem")] public bool IsMultiItem {get; set;}
    [JsonPropertyName("expectedValue")] public decimal ExpectedValue {get; set;}
    [JsonPropertyName("actualValue")] public decimal ActualValue {get; set;}
    [JsonPropertyName("anomalyType")] public string? AnomalyType {get; set;} // e.g., "Missing", "Mismatch"
}