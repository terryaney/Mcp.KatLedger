using System.Text.Json.Serialization;

namespace KatLedger;

internal sealed record CheckWrite(
    string Workspace,
    string TaskId,
    string Phase,
    string CheckName,
    string Tool,
    string? Command,
    int? ExitCode,
    int Passed,
    string? OutputSnippet);

internal sealed record CheckQuery(
    string Workspace,
    string TaskId,
    string? Phase,
    string? CheckName,
    int? Passed,
    int Limit);

internal sealed record CheckRow(
    long Id,
    string Workspace,
    string TaskId,
    string Phase,
    string CheckName,
    string Tool,
    string? Command,
    int? ExitCode,
    int Passed,
    string? OutputSnippet,
    string TimestampUtc);

public sealed record InsertCheckResult(
    string Workspace,
    [property: JsonPropertyName("task_id")] string TaskId,
    long Id,
    string Phase,
    [property: JsonPropertyName("check_name")] string CheckName,
    string Tool,
    int Passed,
    [property: JsonPropertyName("database_path")] string DatabasePath,
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    string TimestampUtc);

public sealed record CountChecksResult(
    string Workspace,
    [property: JsonPropertyName("task_id")] string TaskId,
    int Count,
    string? Phase,
    [property: JsonPropertyName("check_name")] string? CheckName,
    int? Passed,
    [property: JsonPropertyName("database_path")] string DatabasePath,
    [property: JsonPropertyName("schema_version")] int SchemaVersion);

public sealed record ListCheckItem(
    long Id,
    string Phase,
    [property: JsonPropertyName("check_name")] string CheckName,
    string Tool,
    [property: JsonPropertyName("exit_code")] int? ExitCode,
    int Passed,
    [property: JsonPropertyName("output_snippet")] string? OutputSnippet,
    string TimestampUtc);

public sealed record ListChecksResult(
    string Workspace,
    [property: JsonPropertyName("task_id")] string TaskId,
    int Returned,
    string? Phase,
    [property: JsonPropertyName("check_name")] string? CheckName,
    int? Passed,
    [property: JsonPropertyName("database_path")] string DatabasePath,
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    IReadOnlyList<ListCheckItem> Items);

public sealed record ReadCheckItem(
    long Id,
    string Workspace,
    [property: JsonPropertyName("task_id")] string TaskId,
    string Phase,
    [property: JsonPropertyName("check_name")] string CheckName,
    string Tool,
    string? Command,
    [property: JsonPropertyName("exit_code")] int? ExitCode,
    int Passed,
    [property: JsonPropertyName("output_snippet")] string? OutputSnippet,
    string TimestampUtc);

public sealed record ReadChecksResult(
    string Workspace,
    [property: JsonPropertyName("task_id")] string TaskId,
    int Returned,
    string? Phase,
    [property: JsonPropertyName("check_name")] string? CheckName,
    int? Passed,
    [property: JsonPropertyName("database_path")] string DatabasePath,
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    IReadOnlyList<ReadCheckItem> Items);
