namespace ADOToAIOTestsMigration.Models;

public class AioConfig
{
    public string JiraBaseUrl { get; set; } = "";
    public string ApiToken { get; set; } = "";  // AIO Access Token — generate from AIO Tests app (? icon → API Reference)
    public string ProjectKey { get; set; } = "";
}

public class SuiteConfig
{
    public int Id { get; set; }

    /// <summary>Optional folder name override for AIO Tests. If omitted, the ADO suite name is used.</summary>
    public string? FolderName { get; set; }
}

public class MigrationConfig
{
    /// <summary>List of ADO test plan IDs to migrate. Empty = migrate all plans.</summary>
    public List<int> TestPlanIds { get; set; } = new();

    /// <summary>List of ADO test suites to migrate within the selected plans. Empty = migrate all suites.</summary>
    public List<SuiteConfig> TestSuites { get; set; } = new();

    /// <summary>When true, reads from ADO but does not write to AIO Tests.</summary>
    public bool DryRun { get; set; } = true;
}

// --- Requests ---

public class AioFolder
{
    [System.Text.Json.Serialization.JsonPropertyName("ID")]
    public int Id { get; set; }
}

public class AioCreateFolderHierarchyRequest
{
    /// <summary>List of folder names representing the hierarchy (e.g. ["Parent", "Child"]).</summary>
    public List<string> FolderHierarchy { get; set; } = new();

    /// <summary>ID of the base folder. Null = create from top level.</summary>
    public int? BaseFolderId { get; set; }
}

public class AioFolderDetails
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? FolderPath { get; set; }
}

public class AioCreateTestCaseRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public AioPriority? Priority { get; set; }
    public AioFolder? Folder { get; set; }
    public AioScriptType? ScriptType { get; set; }
    public List<AioTestStepRequest>? Steps { get; set; }
    public AioCaseStatus? Status { get; set; }
}

public class AioCaseStatus
{
    public string Name { get; set; } = "";
}

public class AioScriptType
{
    public string Name { get; set; } = "Classic";
}

public class AioPriority
{
    public string Name { get; set; } = "";
}

public class AioTestStepRequest
{
    public string Step { get; set; } = "";
    public string? TestData { get; set; }
    public string? ExpectedResult { get; set; }
    public string StepType { get; set; } = "TEXT";
}

public class AioCreateCycleRequest
{
    public string Title { get; set; } = "";
    public string? Objective { get; set; }
}

public class AioAddTestCasesRequest
{
    public List<AioTestRunUpdate> TestRuns { get; set; } = new();
}

public class AioTestRunUpdate
{
    public string TestCaseKey { get; set; } = "";
    public string TestRunStatus { get; set; } = "Not Run";
}

// --- Responses ---

public class AioTestCaseResponse
{
    public string? Key { get; set; }
    public int? Id { get; set; }
    public string? Title { get; set; }
}

public class AioCycleResponse
{
    public string? Key { get; set; }
    public int? Id { get; set; }
    public string? Title { get; set; }
}
