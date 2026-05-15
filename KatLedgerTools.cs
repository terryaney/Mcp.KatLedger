using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace KatLedger;

[McpServerToolType]
public static class KatLedgerTools
{
    [McpServerTool(
        Name = "kat/ledger/insert_check",
        Title = "Insert KAT ledger check",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        UseStructuredContent = true)]
    [Description("Insert one verification ledger row for a specific workspace and task.")]
    public static InsertCheckResult InsertCheck(
        [Description("Absolute workspace path or unique workspace identifier. Required.")]
        string workspace,
        [Description("Task identifier that scopes all rows for the current task. Required.")]
        string task_id,
        [Description("Verification phase. Allowed values: baseline, after, review.")]
        string phase,
        [Description("Check name such as build, test-api, review-gpt-5.3-codex, or readiness-secrets.")]
        string check_name,
        [Description("Tool or system that produced the check, such as dotnet or ide-get_diagnostics.")]
        string tool,
        [Description("Boolean true/false or integer 1/0. Required.")]
        JsonElement passed,
        [Description("Optional command or operation description, up to 4000 characters.")]
        string? command = null,
        [Description("Optional process exit code.")]
        int? exit_code = null,
        [Description("Optional output snippet, up to 4000 characters.")]
        string? output_snippet = null)
    {
        try
        {
            CheckWrite input = new(
                KatLedgerStore.NormalizeRequired("workspace", workspace, KatLedgerStore.MaxWorkspaceLength),
                KatLedgerStore.NormalizeRequired("task_id", task_id, KatLedgerStore.MaxTaskIdLength),
                KatLedgerStore.NormalizePhase(phase, required: true),
                KatLedgerStore.NormalizeRequired("check_name", check_name, KatLedgerStore.MaxCheckNameLength),
                KatLedgerStore.NormalizeRequired("tool", tool, KatLedgerStore.MaxToolLength),
                KatLedgerStore.NormalizeOptional("command", command, KatLedgerStore.MaxCommandLength),
                exit_code,
                KatLedgerStore.ParsePassedRequired(passed),
                KatLedgerStore.NormalizeOptional("output_snippet", output_snippet, KatLedgerStore.MaxOutputSnippetLength));

            return KatLedgerStore.Current.InsertCheck(input);
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(
        Name = "kat/ledger/count_checks",
        Title = "Count KAT ledger checks",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true)]
    [Description("Count verification ledger rows for a specific workspace and task, with optional phase, check_name, and passed filters.")]
    public static CountChecksResult CountChecks(
        [Description("Absolute workspace path or unique workspace identifier. Required.")]
        string workspace,
        [Description("Task identifier that scopes all rows for the current task. Required.")]
        string task_id,
        [Description("Optional phase filter: baseline, after, review.")]
        string? phase = null,
        [Description("Optional exact check_name filter.")]
        string? check_name = null,
        [Description("Optional passed filter as boolean true/false or integer 1/0.")]
        JsonElement? passed = null)
    {
        try
        {
            CheckQuery query = BuildQuery(workspace, task_id, phase, check_name, passed, 1);
            return new CountChecksResult(
                query.Workspace,
                query.TaskId,
                KatLedgerStore.Current.CountChecks(query),
                query.Phase,
                query.CheckName,
                query.Passed,
                KatLedgerStore.Current.DatabasePath,
                KatLedgerStore.CurrentSchemaVersion);
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(
        Name = "kat/ledger/list_checks",
        Title = "List KAT ledger checks",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true)]
    [Description("List recent verification ledger rows for a specific workspace and task.")]
    public static ListChecksResult ListChecks(
        [Description("Absolute workspace path or unique workspace identifier. Required.")]
        string workspace,
        [Description("Task identifier that scopes all rows for the current task. Required.")]
        string task_id,
        [Description("Optional phase filter: baseline, after, review.")]
        string? phase = null,
        [Description("Optional exact check_name filter.")]
        string? check_name = null,
        [Description("Optional passed filter as boolean true/false or integer 1/0.")]
        JsonElement? passed = null,
        [Description("Optional max rows to return. Default 20. Allowed range 1-200.")]
        int? limit = null)
    {
        try
        {
            CheckQuery query = BuildQuery(workspace, task_id, phase, check_name, passed, KatLedgerStore.NormalizeLimit(limit, KatLedgerStore.DefaultListLimit));
            IReadOnlyList<CheckRow> rows = KatLedgerStore.Current.ListChecks(query, descending: true);
            IReadOnlyList<ListCheckItem> items = rows
                .Select(row => new ListCheckItem(
                    row.Id,
                    row.Phase,
                    row.CheckName,
                    row.Tool,
                    row.ExitCode,
                    row.Passed,
                    row.OutputSnippet,
                    row.TimestampUtc))
                .ToArray();

            return new ListChecksResult(
                query.Workspace,
                query.TaskId,
                items.Count,
                query.Phase,
                query.CheckName,
                query.Passed,
                KatLedgerStore.Current.DatabasePath,
                KatLedgerStore.CurrentSchemaVersion,
                items);
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(
        Name = "kat/ledger/read_checks",
        Title = "Read KAT ledger checks",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true)]
    [Description("Read the full verification ledger payload for a specific workspace and task.")]
    public static ReadChecksResult ReadChecks(
        [Description("Absolute workspace path or unique workspace identifier. Required.")]
        string workspace,
        [Description("Task identifier that scopes all rows for the current task. Required.")]
        string task_id,
        [Description("Optional phase filter: baseline, after, review.")]
        string? phase = null,
        [Description("Optional exact check_name filter.")]
        string? check_name = null,
        [Description("Optional passed filter as boolean true/false or integer 1/0.")]
        JsonElement? passed = null,
        [Description("Optional max rows to return. Default 100. Allowed range 1-200.")]
        int? limit = null)
    {
        try
        {
            CheckQuery query = BuildQuery(workspace, task_id, phase, check_name, passed, KatLedgerStore.NormalizeLimit(limit, KatLedgerStore.DefaultReadLimit));
            IReadOnlyList<CheckRow> rows = KatLedgerStore.Current.ListChecks(query, descending: false);
            IReadOnlyList<ReadCheckItem> items = rows
                .Select(row => new ReadCheckItem(
                    row.Id,
                    row.Workspace,
                    row.TaskId,
                    row.Phase,
                    row.CheckName,
                    row.Tool,
                    row.Command,
                    row.ExitCode,
                    row.Passed,
                    row.OutputSnippet,
                    row.TimestampUtc))
                .ToArray();

            return new ReadChecksResult(
                query.Workspace,
                query.TaskId,
                items.Count,
                query.Phase,
                query.CheckName,
                query.Passed,
                KatLedgerStore.Current.DatabasePath,
                KatLedgerStore.CurrentSchemaVersion,
                items);
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    private static CheckQuery BuildQuery(
        string workspace,
        string taskId,
        string? phase,
        string? checkName,
        JsonElement? passed,
        int limit)
    {
        return new CheckQuery(
            KatLedgerStore.NormalizeRequired("workspace", workspace, KatLedgerStore.MaxWorkspaceLength),
            KatLedgerStore.NormalizeRequired("task_id", taskId, KatLedgerStore.MaxTaskIdLength),
            string.IsNullOrWhiteSpace(phase) ? null : KatLedgerStore.NormalizePhase(phase, required: false),
            KatLedgerStore.NormalizeOptional("check_name", checkName, KatLedgerStore.MaxCheckNameLength),
            KatLedgerStore.ParsePassedOptional(passed),
            limit);
    }
}
