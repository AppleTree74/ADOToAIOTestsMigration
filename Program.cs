using ADOToAIOTestsMigration.Models;
using ADOToAIOTestsMigration.Services;
using Microsoft.Extensions.Configuration;

// ── Configuration ──────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var adoConfig = config.GetSection("Ado").Get<AdoConfig>()
    ?? throw new InvalidOperationException("Missing 'Ado' section in appsettings.json");

var aioConfig = config.GetSection("Aio").Get<AioConfig>()
    ?? throw new InvalidOperationException("Missing 'Aio' section in appsettings.json");

var migrationConfig = config.GetSection("Migration").Get<MigrationConfig>() ?? new MigrationConfig();

// ── Services ───────────────────────────────────────────────────────────────────
var adoService = new AdoService(adoConfig);
var aioService = new AioService(aioConfig);

// ── Migration ──────────────────────────────────────────────────────────────────
Console.WriteLine("=== ADO → AIO Tests Migration ===");
if (migrationConfig.DryRun)
    Console.WriteLine("[DRY RUN] No data will be written to AIO Tests.\n");

var stats = new MigrationStats();

try
{
    // 1. Fetch test plans from ADO
    Console.WriteLine("Fetching test plans from ADO...");
    List<AdoTestPlan> plans;
    if (migrationConfig.TestPlanIds.Count > 0)
    {
        plans = new List<AdoTestPlan>();
        foreach (var planId in migrationConfig.TestPlanIds)
        {
            var plan = await adoService.GetTestPlanByIdAsync(planId);
            if (plan != null)
                plans.Add(plan);
            else
                Console.WriteLine($"[WARN] Test plan ID {planId} not found or not accessible.");
        }
    }
    else
    {
        plans = await adoService.GetTestPlansAsync();
    }

    Console.WriteLine($"Found {plans.Count} plan(s) to migrate.\n");
    stats.TotalPlans = plans.Count;

    foreach (var plan in plans)
    {
        Console.WriteLine($"── Plan: [{plan.Id}] {plan.Name}");

        // 2. Create a corresponding cycle in AIO Tests
        AioCycleResponse? cycleResponse = null;
        if (!migrationConfig.DryRun)
        {
            cycleResponse = await aioService.CreateCycleAsync(new AioCreateCycleRequest
            {
                Title = plan.Name,
                Objective = plan.Description
            });

            if (cycleResponse != null)
                Console.WriteLine($"   Created AIO cycle: {cycleResponse.Key}");
        }

        // 3. Fetch suites for this plan
        var suites = await adoService.GetTestSuitesAsync(plan.Id);
        if (migrationConfig.TestSuites.Count > 0)
            suites = suites.Where(s => migrationConfig.TestSuites.Any(sc => sc.Id == s.Id)).ToList();
        Console.WriteLine($"   {suites.Count} suite(s) found.");

        foreach (var suite in suites)
        {
            Console.WriteLine($"   ├─ Suite: [{suite.Id}] {suite.Name}");

            // 4. Fetch test cases in this suite
            var testCases = await adoService.GetTestCasesInSuiteAsync(plan.Id, suite.Id, suite.Name);
            Console.WriteLine($"   │  {testCases.Count} test case(s).");
            stats.TotalTestCases += testCases.Count;

            var createdKeys = new List<string>();

            foreach (var tc in testCases)
            {
                Console.WriteLine($"   │  · [{tc.Id}] {tc.Title} ({tc.Steps.Count} step(s))");

                if (migrationConfig.DryRun) continue;

                // 5. Map and push to AIO Tests
                var suiteConfig = migrationConfig.TestSuites.FirstOrDefault(sc => sc.Id == suite.Id);
                var folderName = !string.IsNullOrWhiteSpace(suiteConfig?.FolderName) ? suiteConfig.FolderName : suite.Name;
                var aioRequest = MapToAioTestCase(tc, folderName);
                var key = await aioService.CreateTestCaseAsync(aioRequest);

                if (key != null)
                {
                    createdKeys.Add(key);
                    stats.MigratedTestCases++;
                }
                else
                {
                    stats.FailedTestCases++;
                }
            }

            // 6. Link test cases to the cycle
            if (!migrationConfig.DryRun && cycleResponse?.Id != null && createdKeys.Count > 0)
            {
                await aioService.AddTestCasesToCycleAsync(cycleResponse.Id.Value, createdKeys);
                Console.WriteLine($"   │  Linked {createdKeys.Count} test case(s) to cycle {cycleResponse.Key}.");
            }
        }

        Console.WriteLine();
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"\n[FATAL] HTTP error: {ex.Message}");
    Console.WriteLine("Check your OrganizationUrl, Project, and PersonalAccessToken in appsettings.json.");
    Environment.Exit(1);
}
catch (Exception ex)
{
    Console.WriteLine($"\n[FATAL] Unexpected error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

// ── Summary ────────────────────────────────────────────────────────────────────
Console.WriteLine("=== Migration Summary ===");
Console.WriteLine($"Plans processed  : {stats.TotalPlans}");
Console.WriteLine($"Test cases found : {stats.TotalTestCases}");

if (!migrationConfig.DryRun)
{
    Console.WriteLine($"Successfully created : {stats.MigratedTestCases}");
    Console.WriteLine($"Failed               : {stats.FailedTestCases}");
}
else
{
    Console.WriteLine("(Dry run — no changes written to AIO Tests)");
}

Console.WriteLine("\nDone.");

// ── Helpers ────────────────────────────────────────────────────────────────────
static AioCreateTestCaseRequest MapToAioTestCase(AdoTestCase tc, string suiteName)
{
    return new AioCreateTestCaseRequest
    {
        Title = tc.Title,
        Folder = new AioFolder { Name = SanitizeFolderName(suiteName) },
        Description = tc.Description,
        Priority = new AioPriority { Name = MapPriority(tc.Priority) },
        Status = new AioCaseStatus { Name = "Published" },
        ScriptType = tc.Steps.Count > 0 ? new AioScriptType { Name = "Classic" } : null,
        Steps = tc.Steps.Count > 0
            ? tc.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Action))
                .Select(s => new AioTestStepRequest
                {
                    Step = s.Action,
                    TestData = s.TestData,
                    ExpectedResult = s.ExpectedResult.Length > 0 ? s.ExpectedResult : null
                }).ToList()
            : null
    };
}

static string MapPriority(int adoPriority) => adoPriority switch
{
    1 => "Critical",
    2 => "High",
    3 => "Medium",
    4 => "Low",
    _ => "Medium"
};


static string SanitizeFolderName(string name) =>
    string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '(' || c == ')' ? c : '_'));

// ── Stats ──────────────────────────────────────────────────────────────────────
class MigrationStats
{
    public int TotalPlans { get; set; }
    public int TotalTestCases { get; set; }
    public int MigratedTestCases { get; set; }
    public int FailedTestCases { get; set; }
}
