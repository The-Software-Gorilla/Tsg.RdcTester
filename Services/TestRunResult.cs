using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Tsg.Models.Ensenta;
using Tsg.Models.SymX;
using Tsg.RdcTester.Controllers;
using Tsg.RdcTester.Model;

namespace Tsg.RdcTester.Services;

public class TestRunResult
{
    private readonly ILogger<TestRunner> _logger;
    private readonly string _storageConnection;
    public TestRunResult(ILogger<TestRunner> logger, string storageConnection)
    {
        _logger = logger;
        _storageConnection = storageConnection;
    }
    
    public async Task<TestRunResultResponse> CalculateResultsAsync(TestRunResultRequest request)
    {
        Guid reqId = Guid.Parse(request.TestRunId);
        var result = new TestRunResultResponse();
        result.TestRunStatus = await CheckStatusAsync(reqId);
        
        // grab the testCall table to log individual call results
        var callTable = new TableClient(_storageConnection, "testCalls");
        var depositTable = new TableClient(_storageConnection, "depositTransaction");
        var syntheticSymxTable = new TableClient(_storageConnection, "syntheticSymXRequests");
        
        // Query all test calls for this reqId
        var queryResults = callTable.QueryAsync<TableEntity>(e => e.PartitionKey == reqId.ToString());
        
        int totalInvokeTimeMs = 0;
        int totalMultiItemInvokeTimeMs = 0;
        int totalSingleItemInvokeTimeMs = 0;
        int totalDepositTimeMs = 0;
        int totalMultiItemDepositTimeMs = 0;
        int totalSingleItemDepositTimeMs = 0;
        int totalSymxCallTimeMs = 0;
        
        int callCount = 0;
        
        await foreach (var call in queryResults)
        {
            List<string> errors = new List<string>();
            string? status = call.GetString("Status");
            int respStatus = call.GetInt32("ResponseStatusCode") ?? 0;
            bool isSuccess = respStatus >= 200 && respStatus < 300;
            DateTimeOffset? createdUtc = call.GetDateTime("CreatedUtc");
            DateTimeOffset? invokeCompleteUtc = call.GetDateTime("LastUpdatedUtc");
            bool isMultiItem = call.GetBoolean("IsMultiItem") ?? false;
            string? transactionId = call.GetString("TransactionId");
            string? symxId = null;
            callCount++;
            if (invokeCompleteUtc != null && createdUtc != null)
            {
                DepositTransactionDetails details = null;
                SymxCallDetails symx = null;
                DoDepositTransaction? depositReq = null;
                UserDefinedParameters? userParams = null;
                if (transactionId != null)
                {
                    details = await FetchDepositTransactionDetailsAsync(depositTable, transactionId);

                    if (details.Status == "processed" && details.CallResponse != null)
                    {
                        try
                        {
                            var respJson = JsonDocument.Parse(details.CallResponse);
                            if (respJson.RootElement.TryGetProperty("symxCallId", out var Id))
                            {
                                symxId = Id.GetString();
                                symx = await FetchSymxCallDetailsAsync(syntheticSymxTable, Id.GetString());
                                userParams = parseSymxUserParams(symx.Xml);
                                depositReq = parseDepositRequestXml(details.Xml);
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore JSON parsing errors
                            _logger.LogWarning("Failed to parse CallResponse JSON for transactionId {TransactionId}", transactionId);
                        }
                    }
                }
                
                int invokeTime = (int)(invokeCompleteUtc - createdUtc).Value.TotalMilliseconds;
                if (invokeTime > request.MaxInvokeTimeMs)
                {
                    errors.Add($"Max Invoke Time ({request.MaxInvokeTimeMs}) Ms exceeded ({invokeTime} Ms)");
                }
                int totalTime = details != null && details.CallCompleteUtc != null
                    ? (int)(details.CallCompleteUtc - createdUtc).Value.TotalMilliseconds
                    : 0;
                if (totalTime > request.MaxDepositTimeMs)
                {
                    errors.Add($"Max Deposit Time ({request.MaxDepositTimeMs}) Ms exceeded ({totalTime} Ms)");
                }
                int symxTime = symx != null && symx.StartedUtc != null && symx.LastUpdatedUtc != null
                    ? (int)(symx.LastUpdatedUtc - symx.StartedUtc).Value.TotalMilliseconds
                    : 0;
                if (symxTime > request.MaxSymxCallTimeMs)
                {
                    errors.Add($"Max SymX Call Time ({request.MaxSymxCallTimeMs}) Ms exceeded ({symxTime} Ms)");
                }
                if (symx !=null && symx.Xml != null)
                {
                    result.SuccessfulSymxCalls++;
                }
                else
                {
                    result.FailedSymxCalls++;
                }
                totalInvokeTimeMs += invokeTime;
                totalDepositTimeMs += totalTime;
                totalSymxCallTimeMs += symxTime;
                result.MaxDoDepositRequestInvokeTimeMs = Math.Max(invokeTime, result.MaxDoDepositRequestInvokeTimeMs);
                result.MinDoDepositRequestInvokeTimeMs = Math.Min(invokeTime, result.MinDoDepositRequestInvokeTimeMs == 0 ? int.MaxValue : result.MinDoDepositRequestInvokeTimeMs);
                result.MaxDoDepositRequestDurationMs = Math.Max(totalTime, result.MaxDoDepositRequestDurationMs);
                result.MinDoDepositRequestDurationMs = Math.Min(totalTime, result.MinDoDepositRequestDurationMs == 0 ? int.MaxValue : result.MinDoDepositRequestDurationMs);
                result.MaxSymxCallDurationMs = Math.Max(symxTime, result.MaxSymxCallDurationMs);
                result.MinSymxCallDurationMs = Math.Min(symxTime, result.MinSymxCallDurationMs == 0 ? int.MaxValue : result.MinSymxCallDurationMs);
                
                if (isMultiItem)
                {
                    result.MultiItemCount++;
                    result.MaxMultiItemInvokeTimeMs = Math.Max(invokeTime, result.MaxMultiItemInvokeTimeMs);
                    result.MinMultiItemInvokeTimeMs = Math.Min(invokeTime, result.MinMultiItemInvokeTimeMs == 0 ? int.MaxValue : result.MinMultiItemInvokeTimeMs);
                    result.MaxMultiDepositDurationMs = Math.Max(totalTime, result.MaxMultiDepositDurationMs);
                    result.MinMultiDepositDurationMs = Math.Min(totalTime, result.MinMultiDepositDurationMs == 0 ? int.MaxValue : result.MinMultiDepositDurationMs);
                    totalMultiItemInvokeTimeMs += invokeTime;
                    totalMultiItemDepositTimeMs += totalTime;
                }
                else
                {
                    result.SingleItemCount++;
                    result.MaxSingleItemInvokeTimeMs = Math.Max(invokeTime, result.MaxSingleItemInvokeTimeMs);
                    result.MinSingleItemInvokeTimeMs = Math.Min(invokeTime, result.MinSingleItemInvokeTimeMs == 0 ? int.MaxValue : result.MinSingleItemInvokeTimeMs);
                    result.MaxSingleDepositDurationMs = Math.Max(totalTime, result.MaxSingleDepositDurationMs);
                    result.MinSingleDepositDurationMs = Math.Min(totalTime, result.MinSingleDepositDurationMs == 0 ? int.MaxValue : result.MinSingleDepositDurationMs);
                    totalSingleItemInvokeTimeMs += invokeTime;
                    totalSingleItemDepositTimeMs += totalTime;
                }
                
                if (depositReq != null && userParams != null)
                {
                    foreach (var charPar in userParams.RgUserChr)
                    {
                        switch (charPar.Id)
                        {
                            case 1:
                                if (depositReq.AccountHolderNumber != charPar.Value)
                                {
                                    errors.Add($"AccountNumber mismatch: SymX '{charPar.Value}' vs Deposit '{depositReq.AccountHolderNumber}'");
                                }
                                break;
                            case 2:
                                if (depositReq.AcctSuffix.ToUpper().Replace("S","") != charPar.Value)
                                {
                                    errors.Add($"AccountSuffix mismatch: SymX '{charPar.Value}' vs Deposit '{depositReq.AcctSuffix}'");
                                }
                                break;
                            case 3:
                                if (depositReq.ReceiptTransactionNumber != charPar.Value)
                                {
                                    errors.Add($"ReceiptTransactionNumber mismatch: SymX '{charPar.Value}' vs Deposit '{depositReq.ReceiptTransactionNumber}'");
                                }
                                break;
                        }
                    }
                    foreach (var numPar in userParams.RgUserNum)
                    {
                        if (numPar.Id == 1)
                        {
                            if (decimal.TryParse(depositReq.DepositItems.Sum(di => decimal.Parse(di.Amount)).ToString("F2"), out var totalAmount))
                            {
                                if (totalAmount != numPar.Value / 100.0m)
                                {
                                    errors.Add($"Total Amount mismatch: SymX '{numPar.Value / 100.0m:F2}' vs Deposit '{totalAmount:F2}'");
                                }
                            }
                            else
                            {
                                errors.Add($"Failed to parse Deposit total amount for transaction {transactionId}");
                            }
                        }
                    }
                }

            }
            
            if (status == "complete" && isSuccess)
            {
                result.SuccessfulDeposits++;
            }
            else
            {
                errors.Add($"Failed to complete deposits for transaction {transactionId}");
                result.FailedDeposits++;
            }
            
            if (errors.Count > 0)
            {
                if (result.DepositItemAnomalies == null)
                {
                    result.DepositItemAnomalies = new List<TestItemAnomaly>();
                }
                result.DepositItemAnomalies.Add(new TestItemAnomaly()
                {
                    CallId = call.RowKey,
                    CallNumber = call.GetInt32("CallNumber") ?? 0,
                    DepositTransactionId = transactionId,
                    ErrorList = errors,
                    IsMultiItem = isMultiItem,
                    RequestId = reqId.ToString(),
                    SymxOutboundId = symxId
                        
                });
            }
            
        }
        if (result.DepositItemAnomalies != null && result.DepositItemAnomalies.Count > 0)
        {
            result.DepositItemAnomalyCount = result.DepositItemAnomalies.Count;
        }
        result.AvgDoDepositRequestInvokeTimeMs = callCount > 0 ? totalInvokeTimeMs / callCount : 0;
        result.AvgMultiItemInvokeTimeMs = result.MultiItemCount > 0 ? totalMultiItemInvokeTimeMs / result.MultiItemCount : 0;
        result.AvgSingleItemInvokeTimeMs = result.SingleItemCount > 0 ? totalSingleItemInvokeTimeMs / result.SingleItemCount : 0;
        result.AvgDoDepositRequestDurationMs = callCount > 0 ? totalDepositTimeMs / callCount : 0;
        result.AvgMultiDepositDurationMs = result.MultiItemCount > 0 ? totalMultiItemDepositTimeMs / result.MultiItemCount : 0;
        result.AvgSingleDepositDurationMs = result.SingleItemCount > 0 ? totalSingleItemDepositTimeMs / result.SingleItemCount : 0;
        result.AvgSymxCallDurationMs = callCount > 0 ? totalSymxCallTimeMs / callCount : 0;
        
        
        return result;
    }
    
    public async Task<TestRunStatusResponse> CheckStatusAsync(Guid reqId)
    {
        var runTable = new TableClient(_storageConnection, "testRuns");
        var entity = await runTable.GetEntityAsync<TableEntity>("testRun", reqId.ToString());

        int totalCalls = entity.Value.GetInt32("TotalCalls") ?? 0;
        int completedCalls = entity.Value.GetInt32("CompletedCalls") ?? 0;
        string? status = entity.Value.GetString("Status") ?? "unknown";
        DateTimeOffset? startedUtc = entity.Value.GetDateTime("StartedUtc");
        DateTimeOffset? lastUpdatedUtc = entity.Value.GetDateTime("LastUpdatedUtc");
        int duration = 0;

        TimeSpan? durationTimeSpan = null;
        if (startedUtc != null && lastUpdatedUtc != null)
        {
            durationTimeSpan = lastUpdatedUtc - startedUtc;
            duration = (int)durationTimeSpan.Value.TotalSeconds;
        }

        return new TestRunStatusResponse
        {
            TotalCalls = totalCalls,
            CompletedCalls = completedCalls,
            Status = status,
            StartedUtc = startedUtc,
            LastUpdatedUtc = lastUpdatedUtc,
            Duration = duration
        };

    }

    private UserDefinedParameters? parseSymxUserParams(string? xml)
    {
        if (string.IsNullOrEmpty(xml)) return null;
        try
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(SymXSoapEnvelope));
            using var reader = new StringReader(xml);
            SymXSoapEnvelope? envelope = (SymXSoapEnvelope)serializer.Deserialize(reader);
            return envelope.Body.ExecutePowerOnReturnArray.Request.Body.UserDefinedParameters;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to parse SymX SOAP XML");
            return null;
        }
    }
    
    private DoDepositTransaction? parseDepositRequestXml(string? xml)
    {
        if (xml == null) return null;
        try
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(EnsentaRequestSoapEnvelope));
            using var reader = new StringReader(xml);
            EnsentaRequestSoapEnvelope? envelope = (EnsentaRequestSoapEnvelope)serializer.Deserialize(reader);
            return envelope.Body.DoDepositTransaction;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to parse DoDepositTransaction XML");
            return null;
        }
    }
    
    private async Task<DepositTransactionDetails> FetchDepositTransactionDetailsAsync(TableClient depositTable, string reqId)
    {
        TableEntity entity = await depositTable.GetEntityAsync<TableEntity>("deposit", reqId);
        return new DepositTransactionDetails(
            entity.GetDateTime("CallCompleteUtc"),
            entity.GetString("Status"),
            entity.GetDateTime("ReceivedUtc"),
            entity.GetString("Xml"),
            entity.GetString("CallResponse")
        );
            
    }
    
    private async Task<SymxCallDetails> FetchSymxCallDetailsAsync(TableClient symxTable, string callId)
    {
        try
        {
            TableEntity entity = await symxTable.GetEntityAsync<TableEntity>("deposit", callId);
            return new SymxCallDetails(
                entity.GetDateTime("ReceivedUtc"),
                entity.GetDateTime("LastUpdatedUtc"),
                entity.GetString("Xml")
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("No SymX call found for callId {CallId}", callId);
            return new SymxCallDetails(null, null, null);
        }
    }
    
    private record DepositTransactionDetails(DateTimeOffset? CallCompleteUtc, string? Status, DateTimeOffset? ReceivedUtc, string? Xml, string? CallResponse);
    
    private record SymxCallDetails(DateTimeOffset? StartedUtc, DateTimeOffset? LastUpdatedUtc, String? Xml);

}