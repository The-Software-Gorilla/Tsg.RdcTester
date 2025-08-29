using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Tsg.RdcTester.Model;
using Tsg.RdcTester.Services;

namespace Tsg.RdcTester.Controllers;

public class TestRunner : ControllerBase
{
    private const string AzureJobStorage = "AzureWebJobsStorage";
    private const string AcceptedStatus = "accepted";
    private readonly ILogger<TestRunner> _logger;
    private readonly string _storageConnection;
    private readonly IConfiguration _configuration;
    public TestRunner(ILogger<TestRunner> logger, IConfiguration cfg) : base()
    {
        _logger = logger;
        _storageConnection = cfg.GetValue<string>(AzureJobStorage) 
                             ?? throw new InvalidOperationException($"{AzureJobStorage} is not set.");
        _configuration = cfg;
    }
    
    [HttpPost]
    [Route("/run")]
    public async Task<IActionResult> Run([FromServices] IHttpClientFactory httpClientFactory, [FromBody] TestRunRequest? req)
    { 
        Guid reqId = Guid.NewGuid();
        var startTime = DateTimeOffset.UtcNow;
        if (req == null)
        {
            return new BadRequestObjectResult(new { error = "Request body is required" });
        }
        var validation = req.Validate();
        if (!validation.IsValid)
        {
            return new BadRequestObjectResult(new { error = validation.Error });
        }
        
        var runTable = new TableClient(_storageConnection, "testRuns");
        await runTable.CreateIfNotExistsAsync();
        var entity = new TableEntity("testRun", reqId.ToString())
        {
            { "StartedUtc", startTime },
            { "Status", AcceptedStatus },
            { "LastUpdatedUtc", startTime },
            { "TestParameters", JsonSerializer.Serialize(req) }
        };
        await runTable.AddEntityAsync(entity);
        
        // Start a background task to start processing the request. It needs to take the TestRunRequest,
        // the storage connection string, and it needs the configuration to get environment settings
        _ = Task.Run(async () =>
        {
            var processor = new TestRunProcessor(_logger, _storageConnection, _configuration);
            try
            {
                await processor.ProcessAsync(reqId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing test run {ReqId}", reqId);
            }
        });
        

        return new AcceptedResult(string.Empty, new
            {
                status = AcceptedStatus,
                startTime = startTime,
                ts = DateTimeOffset.UtcNow, 
                reqId = reqId
            });
    }

    [HttpGet]
    [Route("/status/{id:guid}")]
    public async Task<IActionResult> Status([FromRoute] Guid id)
    {
        return new OkObjectResult(new
        {
            status = "ok",
            ts = DateTimeOffset.UtcNow,
            reqId = id,
            progress = "not implemented"
        });
    }
}

