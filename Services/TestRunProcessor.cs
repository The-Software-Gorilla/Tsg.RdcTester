using Azure.Data.Tables;
using Tsg.RdcTester.Controllers;
using Tsg.RdcTester.Model;

namespace Tsg.RdcTester.Services;

public class TestRunProcessor
{
    private readonly ILogger<TestRunner> _logger;
    private readonly string _storageConnection;
    private readonly IConfiguration _configuration;
    public TestRunProcessor(ILogger<TestRunner> logger, string storageConnection, IConfiguration cfg)
    {
        _logger = logger;
        _storageConnection = storageConnection;
        _configuration = cfg;
    }

    public async Task ProcessAsync(Guid reqId)
    {
        _logger.LogInformation("Processing request {reqId}", reqId);
        
        // grab the request details from Table storage using reqId
        var runTable = new TableClient(_storageConnection, "testRuns");
        var entity = await runTable.GetEntityAsync<TableEntity>("testRun", reqId.ToString());
        var testParamsJson = entity.Value.GetString("TestParameters");
        var testParams = System.Text.Json.JsonSerializer.Deserialize<TestRunRequest>(testParamsJson ?? throw new InvalidOperationException("TestParameters is null"));
        if (testParams == null)
        {
            throw new InvalidOperationException("Failed to deserialize TestParameters");
        }
        
        // grab the testCall table to log individual call results
        var callTable = new TableClient(_storageConnection, "testCalls");
        await callTable.CreateIfNotExistsAsync();
        // figure out how manny requests to send per second
        int rps = (int)Math.Floor((double)testParams.NumCalls / testParams.DurationSeconds);
        _logger.LogInformation("{TS}: Processing {numCalls} calls over {duration} seconds ({rps} calls/sec)", 
            DateTimeOffset.UtcNow.ToString(), testParams.NumCalls, testParams.DurationSeconds, rps);
        // simulate sending requests at the calculated rate
     
        for (int call = 0; call < testParams.NumCalls; )
        {
            int currentSecond = call / rps;
            _logger.LogInformation("Starting second {second}", currentSecond + 1);
            
            int callsThisSecond = Math.Min(rps, testParams.NumCalls - call);
            for (int i = 0; i < callsThisSecond; i++, call++)
            {
                _logger.LogInformation("{TS}: {ReqId} - Sending request {call} of {numCalls}", DateTimeOffset.UtcNow.ToString(), reqId.ToString(), call + 1, testParams.NumCalls);
            }

            await Task.Delay(1000);
        }
        _logger.LogInformation("{TS}: Completed processing request {reqId}", DateTimeOffset.UtcNow.ToString(), reqId);
    }
}