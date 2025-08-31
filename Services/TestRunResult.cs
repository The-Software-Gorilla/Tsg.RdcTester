using System.Text.Json;
using Azure;
using Azure.Data.Tables;
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
    
    public async Task<TestRunResultResponse> CalculateResultsAsync(Guid reqId)
    {
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
        
        List<string> errors = new List<string>();
        
        await foreach (var call in queryResults)
        {
            string? status = call.GetString("Status");
            int respStatus = call.GetInt32("ResponseStatusCode") ?? 0;
            bool isSuccess = respStatus >= 200 && respStatus < 300;
            DateTimeOffset? createdUtc = call.GetDateTime("CreatedUtc");
            DateTimeOffset? invokeCompleteUtc = call.GetDateTime("LastUpdatedUtc");
            bool isMultiItem = call.GetBoolean("IsMultiItem") ?? false;
            string? transactionId = call.GetString("TransactionId");
            callCount++;
            if (invokeCompleteUtc != null && createdUtc != null)
            {
                DepositTransactionDetails details = null;
                SymxCallDetails symx = null;
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
                                symx = await FetchSymxCallDetailsAsync(syntheticSymxTable, Id.GetString());
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
                int totalTime = details != null && details.CallCompleteUtc != null
                    ? (int)(details.CallCompleteUtc - createdUtc).Value.TotalMilliseconds
                    : 0;
                int symxTime = symx != null && symx.StartedUtc != null && symx.LastUpdatedUtc != null
                    ? (int)(symx.LastUpdatedUtc - symx.StartedUtc).Value.TotalMilliseconds
                    : 0;
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

            }
            
            if (status == "complete" && isSuccess)
            {
                result.SuccessfulDeposits++;
            }
            else
            {
                result.FailedDeposits++;
            }
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