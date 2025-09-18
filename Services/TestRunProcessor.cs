using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using Azure;
using Azure.Data.Tables;
using Tsg.RdcTester.Controllers;
using Tsg.RdcTester.Model;
using Tsg.Models.Ensenta;

namespace Tsg.RdcTester.Services;

public class TestRunProcessor
{
    private readonly ILogger<TestRunner> _logger;
    private readonly string _storageConnection;
    public TestRunProcessor(ILogger<TestRunner> logger, string storageConnection)
    {
        _logger = logger;
        _storageConnection = storageConnection;
    }

    public async Task ProcessAsync(Guid reqId)
    {
        
        // grab the request details from Table storage using reqId
        var runTable = new TableClient(_storageConnection, "testRuns");
        var entity = await runTable.GetEntityAsync<TableEntity>("testRun", reqId.ToString());
        var testParamsJson = entity.Value.GetString("TestParameters");
        var testParams = JsonSerializer.Deserialize<TestRunRequest>(testParamsJson ?? 
            throw new InvalidOperationException("TestParameters is null"));
        if (testParams == null)
        {
            throw new InvalidOperationException("Failed to deserialize TestParameters");
        }
        
        // grab the testCall table to log individual call results
        var callTable = new TableClient(_storageConnection, "testCalls");
        await callTable.CreateIfNotExistsAsync();
        
        // figure out how manny requests to send per second
        _logger.LogInformation("{TS}: {ReqId} - Processing {numCalls} calls over {duration} seconds", 
            DateTimeOffset.UtcNow.ToString(), reqId, testParams.NumCalls, testParams.DurationSeconds);
        
        entity.Value["Status"] = "started";
        entity.Value["TotalCalls"] = testParams.NumCalls;
        entity.Value["CompletedCalls"] = 0;
        entity.Value["LastUpdatedUtc"] = DateTimeOffset.UtcNow;
        await runTable.UpdateEntityAsync(entity.Value, entity.Value.ETag);
        
        // Calculate the interval between calls
        double intervalSeconds = (double)testParams.DurationSeconds / testParams.NumCalls;
        var nextCallTime = DateTimeOffset.UtcNow;
        
        int multiItemCalls = (int)(testParams.NumCalls * ((double)testParams.MultiItemPercentage/100));
        int multiItemInterval = multiItemCalls > 0 ? testParams.NumCalls / Math.Max(multiItemCalls, 1) : 0;
        
        // simulate sending requests at the calculated rate
        for (int call = 0; call < testParams.NumCalls; call++ )
        {
            var startCallTime = DateTimeOffset.UtcNow;
            //Determine if this call should be multi-item
            bool isMultiItem = (multiItemInterval > 0) && (call % multiItemInterval == 0) && (multiItemCalls > 0);
            if (isMultiItem) multiItemCalls--;
            
            //Create an Ensenta SOAP message to send
            var envelope = SerializeEnvelope(CreateTestCallEnvelope(isMultiItem));
            var callId = Guid.NewGuid();
            
            //Create the test call entity and save it to callTable
            var testCallEntity = new TableEntity(reqId.ToString(), callId.ToString())
            {
                { "CallNumber", call + 1 },
                { "IsMultiItem", isMultiItem },
                { "RequestPayload", envelope },
                { "Status", "created" },
                { "CreatedUtc", DateTimeOffset.UtcNow }
            };
            await callTable.AddEntityAsync(testCallEntity);
            
            _logger.LogInformation("{TS}: {ReqId} - Sending request {Call} of {NumCalls}", 
                DateTimeOffset.UtcNow.ToString(), reqId.ToString(), call + 1, testParams.NumCalls);

            //Submit the test call for processing via http request
            var response = await CallEnsentaDoDepositAsync(testParams.TargetUrl, testParams.Headers, envelope);
            
            var responseContent = await response.Content.ReadAsStringAsync();

            string transactionId = string.Empty;
            
            // parse the response as an EnsertaResponseSoapEnvelope to get the transactionId if successful.
            // The responseContent should be a valid SOAP response.
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(EnsentaResponseSoapEnvelope));
                    using var stringReader = new StringReader(responseContent);
                    var responseEnvelope = (EnsentaResponseSoapEnvelope)serializer.Deserialize(stringReader);
                    transactionId = responseEnvelope?.Body?.DoDepositTransactionResponse?.DoDepositTransactionResult?.TransactionId.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{TS}: {ReqId} - Error deserializing response for call {Call}", 
                        DateTimeOffset.UtcNow.ToString(), reqId.ToString(), call + 1);
                }
            }
            
            testCallEntity["ResponsePayload"] = responseContent;
            testCallEntity["ResponseStatusCode"] = (int)response.StatusCode;
            testCallEntity["Status"] = response.IsSuccessStatusCode ? "complete" : "error";
            testCallEntity["LastUpdatedUtc"] = DateTimeOffset.UtcNow;
            testCallEntity["TransactionId"] = transactionId;
            await callTable.UpdateEntityAsync(testCallEntity, ETag.All);
            
            entity.Value["Status"] = "processing";
            entity.Value["CompletedCalls"] = call;
            entity.Value["LastUpdatedUtc"] = DateTimeOffset.UtcNow;
            await runTable.UpdateEntityAsync(entity.Value, ETag.All);
            

            if (call < testParams.NumCalls)
            {
                nextCallTime = nextCallTime.AddSeconds(intervalSeconds);
                var delay = nextCallTime - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay);
                }
            }
        }
        
        entity.Value["Status"] = "complete";
        entity.Value["CompletedCalls"] = testParams.NumCalls;
        entity.Value["LastUpdatedUtc"] = DateTimeOffset.UtcNow;
        await runTable.UpdateEntityAsync(entity.Value, ETag.All);
        _logger.LogInformation("{TS}: Completed processing request {ReqId}", DateTimeOffset.UtcNow.ToString(), reqId);
    }
    
    private async Task<HttpResponseMessage> CallEnsentaDoDepositAsync(string url, Dictionary<string, string> headers, string soapMessage)
    {
        using var httpClient = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }
        request.Content = new StringContent(soapMessage, Encoding.UTF8, "application/xml");
        return await httpClient.SendAsync(request);
    }
    
    private static string SerializeEnvelope(EnsentaRequestSoapEnvelope envelope)
    {
        var serializer = new XmlSerializer(typeof(EnsentaRequestSoapEnvelope));
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        };
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, Encoding.UTF8))
        using (var xmlWriter = XmlWriter.Create(writer, settings))        {
            serializer.Serialize(xmlWriter, envelope);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
    private static EnsentaRequestSoapEnvelope CreateTestCallEnvelope(bool multiDepositItem = false)
    {
        TestAccount testAccount = TestAccount.GetTestAccounts()[Random.Shared.Next(0, TestAccount.GetTestAccounts().Count - 1)];
        var envelope = new EnsentaRequestSoapEnvelope
        {
            Body = new EnsentaRequestSoapBody
            {
                DoDepositTransaction = new DoDepositTransaction
                {
                    AccountHolderNumber = testAccount.AccountNumber,
                    AcctSuffix = testAccount.AccountSuffix,
                    ReceiptTransactionNumber = Random.Shared.Next(100000000, 999999999).ToString("D9"),
                    StationDateTime = DateTime.UtcNow,
                    IsReversalFlag = "N",
                    TransactionType = "Deposit",
                    FeeAmount = "0",
                    DepositItems = CreateDepositItems(multiDepositItem ? Random.Shared.Next(2, 11) : 1)
                    
                }
            }
        };
        return envelope;
    }

    private static List<DepositItem> CreateDepositItems(int numDepositItems = 1)
    {
        List<DepositItem> depositItems = new List<DepositItem>();
        for (int i = 0; i < numDepositItems; i++)
        {
            var item = new DepositItem
            {
                Amount = (Random.Shared.Next(100, 10000) / 100.0).ToString("F2"),
                CodeLine = BuildCodeLine(),
                HostHoldCode = "L",
                FrontFileContents = null,
                BackFileContents = null
            };
            depositItems.Add(item);
        }
        return depositItems;
    }

    private static string BuildCodeLine()
    {
        var routingNumber = Random.Shared.Next(100000000, 999999999).ToString("D9");
        var accountNumber = Random.Shared.Next(10000000, 99999999).ToString("D8");
        var checkNumber = Random.Shared.Next(1000, 9999).ToString("D4");
        return $"V{routingNumber}V T{accountNumber}T{checkNumber}V";
    }
}