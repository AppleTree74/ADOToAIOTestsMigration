namespace ADOToAIOTestsMigration.Models;

public class AdoConfig
{
    public string OrganizationUrl { get; set; } = "";
    public string Project { get; set; } = "";
    public string PersonalAccessToken { get; set; } = "";
}

public class AdoTestPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? AreaPath { get; set; }
}

public class AdoTestSuite
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int PlanId { get; set; }
    public string? SuiteType { get; set; }
    public int? ParentSuiteId { get; set; }
}

public class AdoTestCase
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int Priority { get; set; }
    public string? AreaPath { get; set; }
    public string? Tags { get; set; }
    public string? SuiteName { get; set; }
    public List<AdoTestStep> Steps { get; set; } = [];
}

public class AdoTestStep
{
    public int Order { get; set; }
    public string Action { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string? TestData { get; set; }
    public bool IsSharedStep { get; set; }
}

// ADO REST API response wrappers
public class AdoListResponse<T>
{
    public List<T> Value { get; set; } = new();
    public int Count { get; set; }
}

public class AdoTestPlanResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public AdoAreaReference? Area { get; set; }
}

public class AdoAreaReference
{
    public string? Name { get; set; }
}

public class AdoTestSuiteResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? SuiteType { get; set; }
    public AdoTestSuiteParent? ParentSuite { get; set; }
}

public class AdoTestSuiteParent
{
    public int Id { get; set; }
}

public class AdoSuiteTestCaseResponse
{
    public AdoWorkItemReference? TestCase { get; set; }
    public AdoWorkItemFields? WorkItem { get; set; }
}

public class AdoWorkItemReference
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
}

public class AdoWorkItemFields
{
    public List<Dictionary<string, object?>>? WorkItemFields { get; set; }
}

public class AdoWorkItemResponse
{
    public int Id { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new();
}
