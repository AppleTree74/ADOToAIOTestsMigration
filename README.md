# ADO → AIO Tests Migration Tool

A .NET 10 console application that migrates test plans, test suites, and test cases from **Azure DevOps (ADO)** to **AIO Tests** — the Jira test management plugin by Appfire.

---

## Why This Exists

Azure DevOps and Jira are both widely used platforms, but teams that switch from ADO to Jira have no built-in way to bring their test assets across. Manually recreating hundreds of test cases, steps, priorities, and folder structures is error-prone and time-consuming.

This tool automates the full migration by reading directly from the ADO REST API and writing to the AIO Tests REST API, preserving the structure and content of your test suite as faithfully as possible.

---

## What Gets Migrated

| ADO Concept | AIO Tests Equivalent |
|---|---|
| Test Plan | Cycle |
| Test Suite | Folder (inside the cycle) |
| Test Case | Test Case |
| Test Steps | Steps — action + expected result |
| Priority (1–4) | Priority — Critical / High / Medium / Low |
| Tags | Labels |
| Description | Description |

### What is NOT migrated

- Test results and execution history
- Attachments
- Shared steps *(referenced as a placeholder note)*
- Custom ADO fields beyond the standard set

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Network access to `dev.azure.com` and your Jira instance
- An ADO Personal Access Token with **Test Management → Read** and **Work Items → Read** scopes
- A Jira API token linked to an account that has write access to the target project

---

## Configuration

Create an `appsettings.json` file in the project root (it is git-ignored — never commit credentials):

```json
{
  "Ado": {
    "OrganizationUrl": "https://dev.azure.com/your-org",
    "Project": "YourProject",
    "PersonalAccessToken": "your-ado-pat-here"
  },
  "Aio": {
    "JiraBaseUrl": "https://your-instance.atlassian.net",
    "ApiToken": "your-aio-api-token-here",
    "ProjectKey": "PROJ"
  },
  "Migration": {
    "TestPlanIds": [],
    "TestSuites": [],
    "DryRun": true
  }
}
```

### Field reference

| Field | Description |
|---|---|
| `Ado.OrganizationUrl` | URL of your ADO organisation |
| `Ado.Project` | ADO project name *(case-sensitive)* |
| `Ado.PersonalAccessToken` | PAT with Test Management + Work Items read scopes |
| `Aio.JiraBaseUrl` | Root URL of your Jira instance |
| `Aio.ApiToken` | AIO Tests API token *(generate from the AIO Tests app → ? icon → API Reference)* |
| `Aio.ProjectKey` | Jira project key as shown in issue keys, e.g. `QA` |
| `Migration.TestPlanIds` | ADO plan IDs to migrate. Empty array = all plans |
| `Migration.TestSuites` | Specific suites to migrate within the selected plans. Empty array = all suites |
| `Migration.DryRun` | `true` = read-only preview, nothing is written to AIO |

### Targeting specific plans

```json
"TestPlanIds": [12, 34, 56]
```

Leave the array empty to migrate every plan in the project.

### Targeting specific suites

```json
"TestSuites": [
  { "Id": 124897 },
  { "Id": 125161, "FolderName": "Custom Folder Name" }
]
```

- Omit `TestSuites` or leave it empty to include all suites in the selected plans.
- The optional `FolderName` overrides the ADO suite name for the folder created in AIO Tests. Useful when the ADO name needs cleaning up or you simply want a different label in Jira.

---

## Running the Tool

### 1. Dry run first

Always start with `DryRun: true`. This reads everything from ADO and prints a full preview without touching AIO Tests.

```bash
dotnet run
```

Review the output — check plan names, suite names, test case counts, and step counts.

### 2. Live run

Once satisfied, set `DryRun: false` and run again:

```bash
dotnet run
```

A summary is printed at the end:

```
=== Migration Summary ===
Plans processed      : 3
Test cases found     : 147
Successfully created : 145
Failed               : 2
```

### Console output reference

```
── Plan: [12] Regression Tests          ← ADO test plan being processed
   Created AIO cycle: QA-CY-1           ← cycle created in AIO Tests
   ├─ Suite: [45] Login Tests           ← suite (becomes a folder)
   │  3 test case(s).
   │  · [101] Verify login flow (4 step(s))
   │  Linked 3 test case(s) to cycle QA-CY-1.
[ERROR] Failed to create test case ...  ← single failure, migration continues
```

Individual test case failures are logged but do not halt the migration.

---

## Architecture

```
Program.cs            Orchestration — loads config, drives the migration workflow
Services/
  AdoService.cs       ADO REST API client (reads plans, suites, test cases)
  AioService.cs       AIO Tests REST API client (creates cases, cycles, links)
Models/
  AdoModels.cs        ADO domain types, config models, and API response wrappers
  AioModels.cs        AIO domain types, request/response types, and MigrationConfig
```

### Migration flow

```
Fetch ADO test plans (specific IDs or all)
  └─ Create AIO cycle (one per plan)
      └─ Fetch suites → filter if TestSuites configured
          └─ Fetch test cases → map to AIO format → create in AIO
              └─ Batch-link all created cases to the cycle
```

### Key implementation details

- **ADO pagination** — continuation tokens handle large result sets; work items are batch-fetched in groups of 200 (API limit).
- **Test step parsing** — ADO stores steps as XML with HTML-encoded content. A custom regex-based converter handles `<br>`, block tags, lists, and HTML entities.
- **Priority mapping** — ADO numeric priorities 1–4 map to AIO names Critical, High, Medium, Low. Anything else maps to Medium.
- **Folder name sanitisation** — strips characters invalid for AIO folder names, keeping alphanumerics, spaces, dashes, underscores, and parentheses.
- **Error resilience** — individual test case failures are caught and logged; the migration continues and reports a final failure count.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `401 Unauthorized` (ADO) | Expired or invalid PAT | Generate a new PAT with the correct scopes |
| `401 Unauthorized` (AIO) | Wrong API token | Re-check `Aio.ApiToken` in `appsettings.json` |
| `404 Not Found` (ADO) | Wrong org URL or project name | Project name is case-sensitive |
| 0 plans found | PAT missing Test Management read scope | Recreate the PAT with the correct scope |
| Test cases found but 0 steps | Steps are shared steps | Shared step content is not expanded — add manually in AIO if needed |
| `appsettings.json not found` | Running from the wrong directory | `cd` into the project folder before `dotnet run` |
| Build error | .NET 10 SDK not installed | Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |

To capture full output for diagnosis:

```bash
dotnet run > output.txt 2>&1
```
