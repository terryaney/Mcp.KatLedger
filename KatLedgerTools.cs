using System.ComponentModel;
using Microsoft.Data.Sqlite;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace KatLedger;

[McpServerToolType]
public static class KatLedgerTools
{
    [McpServerTool(
        Name = "execute",
        Title = "Execute SQLite statement",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        UseStructuredContent = true)]
    [Description("Execute one non-SELECT SQLite statement against the canonical KatLedger database.")]
    public static ExecuteResult Execute(
        [Description("Single SQLite statement. ATTACH, DETACH, SELECT, WITH, and multi-statement SQL are rejected.")]
        string sql)
    {
        return Invoke(() => KatLedgerStore.Current.Execute(sql));
    }

    [McpServerTool(
        Name = "query",
        Title = "Query SQLite rows",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true)]
    [Description("Run one SELECT or WITH SQLite statement against the canonical KatLedger database.")]
    public static QueryResult Query(
        [Description("Single SELECT or WITH statement. ATTACH, DETACH, and multi-statement SQL are rejected.")]
        string sql,
        [Description("Optional maximum rows to return. Default 50. Allowed range 1-200. The server always enforces the cap.")]
        int? limit = null)
    {
        return Invoke(() => KatLedgerStore.Current.Query(sql, KatLedgerStore.NormalizeLimit(limit)));
    }

    [McpServerTool(
        Name = "query_one",
        Title = "Query one SQLite row",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true)]
    [Description("Run one SELECT or WITH SQLite statement and return at most one row.")]
    public static QueryOneResult QueryOne(
        [Description("Single SELECT or WITH statement. ATTACH, DETACH, and multi-statement SQL are rejected.")]
        string sql)
    {
        return Invoke(() => KatLedgerStore.Current.QueryOne(sql));
    }

    private static T Invoke<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
        catch (SqliteException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}
