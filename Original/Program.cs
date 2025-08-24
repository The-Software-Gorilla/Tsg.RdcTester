// using System.Collections.Concurrent;
// using System.Diagnostics;
// using System.Security.Cryptography;
// using System.Text.Json;
// using System.Text.Json.Serialization;
// using Tsg.Models.Ensenta;
// using Tsg.RdcTester.Original;
//
//
//
//
// // Move CombineUrl above usage
// static string CombineUrl(string baseUrl, string endpoint)
// {
//     if (string.IsNullOrWhiteSpace(endpoint)) return baseUrl;
//     return baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
// }
//
// static string? Trim(string? s, int max)
// {
//     if (s is null) return null;
//     return s.Length <= max ? s : s.Substring(0, max);
// }
//
// // Execute a test run synchronously and return a JSON report
// app.MapPost("/run", async (IHttpClientFactory httpClientFactory, TestRunRequest req) =>
// {
//     var validation = req.Validate();
//     if (!validation.IsValid)
//     {
//         return Results.BadRequest(new { error = validation.Error });
//     }
//
//     var http = httpClientFactory.CreateClient("rdctester");
//     http.Timeout = TimeSpan.FromSeconds(req.TimeoutSeconds);
//
//     var cts = new CancellationTokenSource(TimeSpan.FromSeconds(req.TimeoutSeconds + req.DurationSeconds + 30));
//
//     var schedule = Scheduler.BuildSchedule(req.NumCalls, TimeSpan.FromSeconds(req.DurationSeconds));
//     var throttler = new SemaphoreSlim(Math.Max(1, req.MaxConcurrency));
//
//     var results = new ConcurrentBag<SingleCallResult>();
//
//     var swTotal = Stopwatch.StartNew();
//
//     var tasks = schedule.Select(async (offset, idx) =>
//     {
//         await Task.Delay(offset, cts.Token);
//         await throttler.WaitAsync(cts.Token);
//         try
//         {
//             var url = CombineUrl(req.TargetBaseUrl!, req.Endpoint!);
//             using var payloadDoc = PayloadBuilder.BuildJson(req.PayloadTemplate!, req.Pools);
//             var method = new HttpMethod(req.HttpMethod ?? "POST");
//             using var msg = new HttpRequestMessage(method, url)
//             {
//                 Content = req.HttpMethod?.ToUpperInvariant() == "GET" ? null : JsonContent.Create(payloadDoc.RootElement.Clone())
//             };
//
//             if (req.Headers is not null)
//             {
//                 foreach (var kvp in req.Headers)
//                 {
//                     if (!msg.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value))
//                     {
//                         // fall back to content header
//                         msg.Content?.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
//                     }
//                 }
//             }
//
//             var sw = Stopwatch.StartNew();
//             HttpResponseMessage resp;
//             string? body = null;
//             try
//             {
//                 resp = await http.SendAsync(msg, cts.Token);
//                 body = await resp.Content.ReadAsStringAsync(cts.Token);
//             }
//             catch (Exception ex)
//             {
//                 results.Add(new SingleCallResult
//                 {
//                     Index = idx,
//                     StartedAtUtc = DateTimeOffset.UtcNow,
//                     DurationMs = sw.ElapsedMilliseconds,
//                     StatusCode = 0,
//                     Error = ex.GetType().Name + ": " + ex.Message
//                 });
//                 return;
//             }
//             sw.Stop();
//
//             results.Add(new SingleCallResult
//             {
//                 Index = idx,
//                 StartedAtUtc = DateTimeOffset.UtcNow,
//                 DurationMs = sw.ElapsedMilliseconds,
//                 StatusCode = (int)resp.StatusCode,
//                 Error = resp.IsSuccessStatusCode ? null : (string?)$"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} – {Trim(body, 500)}",
//                 ResponseSnippet = Trim(body, 500)
//             });
//         }
//         finally
//         {
//             throttler.Release();
//         }
//     }).ToArray();
//
//     await Task.WhenAll(tasks);
//     swTotal.Stop();
//
//     var report = TestRunReport.From(results.ToList(), swTotal.Elapsed, req);
//     return Results.Ok(report);
// }).WithTags("run");
//
//
// namespace Tsg.RdcTester.Original
// {
//     // ===== Models =====
//
//     public record TestRunRequest
//     {
//         [JsonPropertyName("targetBaseUrl")] public string? TargetBaseUrl { get; init; }
//         [JsonPropertyName("endpoint")] public string? Endpoint { get; init; }
//         [JsonPropertyName("httpMethod")] public string? HttpMethod { get; init; } = "POST"; // POST, GET, PUT, DELETE
//         [JsonPropertyName("headers")] public Dictionary<string, string>? Headers { get; init; }
//
//         [JsonPropertyName("numCalls")] public int NumCalls { get; init; }
//         [JsonPropertyName("durationSeconds")] public int DurationSeconds { get; init; }
//         [JsonPropertyName("maxConcurrency")] public int MaxConcurrency { get; init; } = 50;
//         [JsonPropertyName("timeoutSeconds")] public int TimeoutSeconds { get; init; } = 100;
//
//         [JsonPropertyName("pools")] public Dictionary<string, JsonElement>? Pools { get; init; } // named lists for random selection
//
//         [JsonPropertyName("payloadTemplate")] public JsonDocument? PayloadTemplate { get; init; } // JSON with placeholders
//
//         public (bool IsValid, string? Error) Validate()
//         {
//             if (string.IsNullOrWhiteSpace(TargetBaseUrl)) return (false, "targetBaseUrl is required");
//             if (string.IsNullOrWhiteSpace(Endpoint)) return (false, "endpoint is required");
//             if (NumCalls <= 0) return (false, "numCalls must be > 0");
//             if (DurationSeconds < 0) return (false, "durationSeconds must be >= 0");
//             if (PayloadTemplate is null && (HttpMethod?.ToUpperInvariant() != "GET")) return (false, "payloadTemplate is required for non-GET methods");
//             return (true, null);
//         }
//     }
//
//     public record SingleCallResult
//     {
//         public int Index { get; init; }
//         public DateTimeOffset StartedAtUtc { get; init; }
//         public long DurationMs { get; init; }
//         public int StatusCode { get; init; }
//         public string? Error { get; init; }
//         public string? ResponseSnippet { get; init; }
//     }
//
//     public record TestRunReport
//     {
//         public string Target { get; init; } = string.Empty;
//         public string Method { get; init; } = string.Empty;
//         public int Requested { get; init; }
//         public int Completed { get; init; }
//         public int Successes { get; init; }
//         public int Failures { get; init; }
//         public double ThroughputPerSec { get; init; }
//         public long TotalDurationMs { get; init; }
//         public long P50Ms { get; init; }
//         public long P95Ms { get; init; }
//         public long P99Ms { get; init; }
//         public Dictionary<int,int> StatusCounts { get; init; } = new();
//         public List<SingleCallResult> Samples { get; init; } = new();
//
//         public static TestRunReport From(List<SingleCallResult> results, TimeSpan total, TestRunRequest req)
//         {
//             var completed = results.Count;
//             var successes = results.Count(r => r.StatusCode is >= 200 and < 300);
//             var failures = completed - successes;
//             var statusCounts = results.GroupBy(r => r.StatusCode).ToDictionary(g => g.Key, g => g.Count());
//             var durations = results.Select(r => r.DurationMs).OrderBy(x => x).ToArray();
//             long Percentile(double p)
//             {
//                 if (durations.Length == 0) return 0;
//                 var rank = (int)Math.Ceiling(p * durations.Length) - 1;
//                 rank = Math.Clamp(rank, 0, durations.Length - 1);
//                 return durations[rank];
//             }
//             return new TestRunReport
//             {
//                 Target = UrlHelper.CombineUrl(req.TargetBaseUrl!, req.Endpoint!),
//                 Method = req.HttpMethod ?? "POST",
//                 Requested = req.NumCalls,
//                 Completed = completed,
//                 Successes = successes,
//                 Failures = failures,
//                 ThroughputPerSec = completed / Math.Max(0.0001, total.TotalSeconds),
//                 TotalDurationMs = (long)total.TotalMilliseconds,
//                 P50Ms = Percentile(0.50),
//                 P95Ms = Percentile(0.95),
//                 P99Ms = Percentile(0.99),
//                 StatusCounts = statusCounts,
//                 Samples = results.Take(20).ToList() // include a small sample for quick inspection
//             };
//         }
//     }
//
// // ===== Scheduling & Payload helpers =====
//
//     static class Scheduler
//     {
//         // Uniformly spread N calls over span. Returns per-call offsets.
//         public static TimeSpan[] BuildSchedule(int count, TimeSpan span)
//         {
//             if (span <= TimeSpan.Zero) return Enumerable.Repeat(TimeSpan.Zero, count).ToArray();
//             var dt = span.TotalMilliseconds / count;
//             return Enumerable.Range(0, count).Select(i => TimeSpan.FromMilliseconds(i * dt)).ToArray();
//         }
//     }
//
//     static class PayloadBuilder
//     {
//         private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
//
//         public static JsonDocument BuildJson(JsonDocument template, Dictionary<string, JsonElement>? pools)
//         {
//             using var stream = new MemoryStream();
//             using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
//             Rewrite(template.RootElement, pools ?? new(), writer);
//             writer.Flush();
//             return JsonDocument.Parse(stream.ToArray());
//         }
//
//         private static void Rewrite(JsonElement element, Dictionary<string, JsonElement> pools, Utf8JsonWriter w)
//         {
//             switch (element.ValueKind)
//             {
//                 case JsonValueKind.Object:
//                     w.WriteStartObject();
//                     foreach (var prop in element.EnumerateObject())
//                     {
//                         w.WritePropertyName(prop.Name);
//                         Rewrite(prop.Value, pools, w);
//                     }
//                     w.WriteEndObject();
//                     break;
//                 case JsonValueKind.Array:
//                     w.WriteStartArray();
//                     foreach (var item in element.EnumerateArray())
//                         Rewrite(item, pools, w);
//                     w.WriteEndArray();
//                     break;
//                 case JsonValueKind.String:
//                     var s = element.GetString() ?? string.Empty;
//                     var replaced = ReplaceTokens(s, pools);
//                     if (double.TryParse(replaced, out var num))
//                     {
//                         w.WriteNumberValue(num);
//                     }
//                     else
//                     {
//                         w.WriteStringValue(replaced);
//                     }
//                     break;
//                 default:
//                     element.WriteTo(w);
//                     break;
//             }
//         }
//
//         private static string ReplaceTokens(string s, Dictionary<string, JsonElement> pools)
//         {
//             // {{poolName}} -> random element from a named pool array
//             // ${GUID} -> new Guid
//             // ${RANDOM_BASE64:N} -> N random bytes, base64-encoded
//             if (s.Contains("{{"))
//             {
//                 foreach (var kv in pools)
//                 {
//                     var token = "{{" + kv.Key + "}}";
//                     if (s.Contains(token))
//                     {
//                         var replacement = PickFromPool(kv.Value);
//                         s = s.Replace(token, replacement);
//                     }
//                 }
//             }
//             if (s.Contains("${GUID}"))
//             {
//                 s = s.Replace("${GUID}", Guid.NewGuid().ToString());
//             }
//             if (s.Contains("${RANDOM_BASE64:"))
//             {
//                 // parse pattern occurrences
//                 int start;
//                 while ((start = s.IndexOf("${RANDOM_BASE64:")) >= 0)
//                 {
//                     var end = s.IndexOf('}', start + 1);
//                     if (end < 0) break;
//                     var inside = s.Substring(start + 16, end - (start + 16));
//                     if (int.TryParse(inside, out var n) && n > 0)
//                     {
//                         var bytes = new byte[n];
//                         _rng.GetBytes(bytes);
//                         var b64 = Convert.ToBase64String(bytes);
//                         s = s.Remove(start, end - start + 1).Insert(start, b64);
//                     }
//                     else
//                     {
//                         break;
//                     }
//                 }
//             }
//             return s;
//         }
//
//         private static string PickFromPool(JsonElement pool)
//         {
//             if (pool.ValueKind != JsonValueKind.Array || pool.GetArrayLength() == 0)
//                 return string.Empty;
//             var idx = Random.Shared.Next(pool.GetArrayLength());
//             var el = pool[idx];
//             return el.ValueKind switch
//             {
//                 JsonValueKind.String => el.GetString() ?? string.Empty,
//                 JsonValueKind.Number => el.ToString(),
//                 _ => el.ToString()
//             };
//         }
//     }
//
//     public static class UrlHelper
//     {
//         public static string CombineUrl(string baseUrl, string endpoint)
//         {
//             if (string.IsNullOrWhiteSpace(endpoint)) return baseUrl;
//             return baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
//         }
//     }
//
//     public class CallPayload
//     {
//         public EnsentaSoapEnvelope Envelope { get;} = new EnsentaSoapEnvelope();
//         public static CallPayload GenerateCallPayload()
//         {
//             return new CallPayload
//             {
//             
//             };
//         }
//     }
// }