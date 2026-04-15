using ADOToAIOTestsMigration.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ADOToAIOTestsMigration.Services;

public class AioService
{
    private readonly HttpClient _client;
    private readonly AioConfig _config;

    private const string AioBaseUrl = "https://tcms.aiojiraapps.com/aio-tcms/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AioService(AioConfig config)
    {
        _config = config;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("AioAuth", config.ApiToken);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Creates a test case in AIO Tests and returns its key (e.g. "PAIO-TC-1").</summary>
    public async Task<string?> CreateTestCaseAsync(AioCreateTestCaseRequest request)
    {
        var url = $"{AioBaseUrl}/project/{_config.ProjectKey}/testcase";
        var body = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"    [ERROR] Failed to create test case '{request.Title}': {response.StatusCode} — {error}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
        return root?["key"]?.GetValue<string>();
    }

    /// <summary>Creates a test cycle and returns the cycle response containing the key and numeric ID.</summary>
    public async Task<AioCycleResponse?> CreateCycleAsync(AioCreateCycleRequest request)
    {
        var url = $"{AioBaseUrl}/project/{_config.ProjectKey}/testcycle/detail";
        var body = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"  [ERROR] Failed to create cycle '{request.Title}': {response.StatusCode} — {error}");
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AioCycleResponse>(json, JsonOptions);
    }

    /// <summary>Adds a batch of test case keys to an existing cycle by its numeric ID.</summary>
    public async Task AddTestCasesToCycleAsync(int cycleId, List<string> testCaseKeys)
    {
        if (testCaseKeys.Count == 0) return;

        var url = $"{AioBaseUrl}/project/{_config.ProjectKey}/testcycle/{cycleId}/bulk/testrun/update";
        var request = new AioAddTestCasesRequest
        {
            TestRuns = testCaseKeys.Select(k => new AioTestRunUpdate { TestCaseKey = k }).ToList()
        };
        var body = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"  [ERROR] Failed to add {testCaseKeys.Count} test case(s) to cycle '{cycleId}': {response.StatusCode} — {responseBody}");
            return;
        }

        // The API returns HTTP 200 even on application-level failures; check the status field.
        var root = JsonSerializer.Deserialize<JsonObject>(responseBody, JsonOptions);
        var status = root?["status"]?.GetValue<string>();
        if (status == "FAILED")
        {
            Console.WriteLine($"  [ERROR] Bulk link returned FAILED for cycle '{cycleId}': {responseBody}");
        }
    }
}
