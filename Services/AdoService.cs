using ADOToAIOTestsMigration.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ADOToAIOTestsMigration.Services;

public class AdoService
{
    private readonly HttpClient _client;
    private readonly AdoConfig _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdoService(AdoConfig config)
    {
        _config = config;

        _client = new HttpClient();
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{config.PersonalAccessToken}"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<AdoTestPlan>> GetTestPlansAsync()
    {
        var plans = new List<AdoTestPlan>();
        string? continuationToken = null;

        do
        {
            var url = $"{_config.OrganizationUrl}/{Uri.EscapeDataString(_config.Project)}/_apis/testplan/plans?api-version=7.1";
            if (continuationToken != null)
                url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";

            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
            var value = root?["value"]?.AsArray();

            if (value != null)
            {
                foreach (var item in value)
                {
                    plans.Add(new AdoTestPlan
                    {
                        Id = item?["id"]?.GetValue<int>() ?? 0,
                        Name = item?["name"]?.GetValue<string>() ?? "",
                        Description = item?["description"]?.GetValue<string>()
                    });
                }
            }

            response.Headers.TryGetValues("x-ms-continuationtoken", out var tokens);
            continuationToken = tokens?.FirstOrDefault();
        }
        while (continuationToken != null);

        return plans;
    }

    public async Task<AdoTestPlan?> GetTestPlanByIdAsync(int planId)
    {
        var url =
            $"{_config.OrganizationUrl}/{Uri.EscapeDataString(_config.Project)}/_apis/testplan/plans/{planId}?api-version=7.1";
        var response = await _client.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var item = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
        if (item == null) return null;

        return new AdoTestPlan
        {
            Id = item["id"]?.GetValue<int>() ?? 0,
            Name = item["name"]?.GetValue<string>() ?? "",
            Description = item["description"]?.GetValue<string>()
        };
    }

    public async Task<List<AdoTestSuite>> GetTestSuitesAsync(int planId)
    {
        var suites = new List<AdoTestSuite>();
        string? continuationToken = null;

        do
        {
            var url = $"{_config.OrganizationUrl}/{Uri.EscapeDataString(_config.Project)}/_apis/testplan/plans/{planId}/suites?api-version=7.1";
            if (continuationToken != null)
                url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";

            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
            var value = root?["value"]?.AsArray();

            if (value != null)
            {
                foreach (var item in value)
                {
                    suites.Add(new AdoTestSuite
                    {
                        Id = item?["id"]?.GetValue<int>() ?? 0,
                        Name = item?["name"]?.GetValue<string>() ?? ""
                    });
                }
            }

            response.Headers.TryGetValues("x-ms-continuationtoken", out var tokens);
            continuationToken = tokens?.FirstOrDefault();
        }
        while (continuationToken != null);

        return suites;
    }

    public async Task<List<AdoTestCase>> GetTestCasesInSuiteAsync(int planId, int suiteId, string suiteName)
    {
        var url =
            $"{_config.OrganizationUrl}/{Uri.EscapeDataString(_config.Project)}/_apis/testplan/plans/{planId}/suites/{suiteId}/testcase?api-version=7.1";
        var response = await _client.GetAsync(url);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return new List<AdoTestCase>();

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
        var value = root?["value"]?.AsArray();

        if (value == null || value.Count == 0) return new List<AdoTestCase>();

        // Collect work item IDs then batch-fetch details.
        // ADO may return testCase.id as a string or integer, so handle both.
        var workItemIds = new List<int>();
        foreach (var item in value)
        {
            var idNode = item?["testCase"]?["id"];
            if (idNode == null)
            {
                // Fallback: some API versions embed the work item id directly
                idNode = item?["workItem"]?["id"] ?? item?["id"];
            }

            if (idNode == null) continue;

            int id;
            if (idNode.GetValueKind() == JsonValueKind.Number)
                id = idNode.GetValue<int>();
            else if (!int.TryParse(idNode.GetValue<string>(), out id))
                continue;

            if (id > 0)
                workItemIds.Add(id);
        }

        if (workItemIds.Count == 0)
        {
            // Debug: dump first item so the caller can see what ADO returned
            Console.WriteLine(
                $"   │  [WARN] Suite {suiteId}: API returned {value.Count} item(s) but no work item IDs could be extracted.");
            if (value.Count > 0)
                Console.WriteLine($"   │  [DEBUG] First item: {value[0]?.ToJsonString()}");
            return new List<AdoTestCase>();
        }

        return await GetWorkItemsAsTestCasesAsync(workItemIds);
    }

    private async Task<List<AdoTestCase>> GetWorkItemsAsTestCasesAsync(List<int> ids)
    {
        // ADO allows up to 200 IDs per batch request
        const int batchSize = 200;
        var result = new List<AdoTestCase>();

        for (int i = 0; i < ids.Count; i += batchSize)
        {
            var batch = ids.Skip(i).Take(batchSize);
            var idList = string.Join(",", batch);
            var url =
                $"{_config.OrganizationUrl}/{Uri.EscapeDataString(_config.Project)}/_apis/wit/workitems?ids={idList}&$expand=all&api-version=7.1";

            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonSerializer.Deserialize<JsonObject>(json, JsonOptions);
            var value = root?["value"]?.AsArray();

            if (value == null) continue;

            foreach (var item in value)
            {
                var fields = item?["fields"];
                if (fields == null) continue;

                var tc = new AdoTestCase
                {
                    Id = item?["id"]?.GetValue<int>() ?? 0,
                    Title = fields["System.Title"]?.GetValue<string>() ?? "(untitled)",
                    Description = HtmlToPlainText(fields["System.Description"]?.GetValue<string>()),
                    Tags = fields["System.Tags"]?.GetValue<string>()
                };

                if (int.TryParse(fields["Microsoft.VSTS.Common.Priority"]?.ToString(), out var priority))
                    tc.Priority = priority;

                var stepsXml = fields["Microsoft.VSTS.TCM.Steps"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(stepsXml))
                    tc.Steps = ParseStepsXml(stepsXml);

                result.Add(tc);
            }
        }

        return result;
    }

    // ADO stores steps as XML with HTML-encoded inner content
    private static List<AdoTestStep> ParseStepsXml(string stepsXml)
    {
        var steps = new List<AdoTestStep>();
        try
        {
            var doc = XDocument.Parse(stepsXml);

            steps.AddRange(from stepEl in doc.Descendants("step")
                let strings = stepEl.Elements("parameterizedString").ToList()
                let action = strings.Count > 0 ? HtmlToPlainText(strings[0].Value) : ""
                let expected = strings.Count > 1 ? HtmlToPlainText(strings[1].Value) : ""
                let isShared = stepEl.Attribute("type")?.Value == "ValidateStep"
                select new AdoTestStep
                    { Action = action ?? "", ExpectedResult = expected ?? "", IsSharedStep = isShared });

            // Handle shared step references
            steps.AddRange(doc.Descendants("compref").Select(compRef => new AdoTestStep
                { Action = $"[Shared Step Ref: {compRef.Attribute("ref")?.Value}]", IsSharedStep = true }));
        }
        catch
        {
            // If XML parsing fails, return a single step with raw content
            steps.Add(new AdoTestStep { Action = HtmlToPlainText(stepsXml) ?? stepsXml });
        }

        return steps;
    }

    private static string? HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        // Block-level tags → newline
        var text = Regex.Replace(html, @"<(br|BR)\s*/?>", "\n");
        text = Regex.Replace(text, @"</(p|div|h[1-6]|tr)>", "\n", RegexOptions.IgnoreCase);

        // List items → bullet
        text = Regex.Replace(text, @"<li\b[^>]*>", "\n• ", RegexOptions.IgnoreCase);

        // Strip remaining tags
        text = Regex.Replace(text, "<[^>]+>", "");

        // Decode HTML entities (e.g. &amp; → &, &nbsp; → space)
        text = System.Net.WebUtility.HtmlDecode(text);

        // Collapse 3+ consecutive newlines to 2, trim
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

}