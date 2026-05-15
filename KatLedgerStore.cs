using System.Text;
using Microsoft.Data.Sqlite;

namespace KatLedger;

internal sealed class KatLedgerStore
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxWorkspaceLength = 1024;
    public const int MaxTaskIdLength = 200;
    public const int MaxPhaseLength = 32;
    public const int MaxCheckNameLength = 200;
    public const int MaxToolLength = 100;
    public const int MaxCommandLength = 4000;
    public const int MaxOutputSnippetLength = 4000;
    public const int DefaultListLimit = 20;
    public const int DefaultReadLimit = 100;
    public const int MaxLimit = 200;

    private static readonly HashSet<string> ValidPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "baseline",
        "after",
        "review"
    };

    public static KatLedgerStore Current { get; private set; } = null!;

    public string DatabasePath { get; }

    private string ConnectionString { get; }

    private KatLedgerStore(string databasePath)
    {
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public static KatLedgerStore OpenCanonical()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string directory = Path.Combine(userProfile, ".kat", "KatLedger");
        string databasePath = Path.Combine(directory, "KatLedger.db");

        Directory.CreateDirectory(directory);

        KatLedgerStore store = new(databasePath);
        store.Bootstrap();
        Current = store;
        return store;
    }

    public InsertCheckResult InsertCheck(CheckWrite input)
    {
        string timestampUtc = DateTimeOffset.UtcNow.ToString("O");

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO anvil_checks (
                workspace,
                task_id,
                phase,
                check_name,
                tool,
                command,
                exit_code,
                output_snippet,
                passed,
                ts
            )
            VALUES (
                @workspace,
                @task_id,
                @phase,
                @check_name,
                @tool,
                @command,
                @exit_code,
                @output_snippet,
                @passed,
                @ts
            );
            SELECT last_insert_rowid();
            """;

        AddParameter(command, "@workspace", input.Workspace);
        AddParameter(command, "@task_id", input.TaskId);
        AddParameter(command, "@phase", input.Phase);
        AddParameter(command, "@check_name", input.CheckName);
        AddParameter(command, "@tool", input.Tool);
        AddParameter(command, "@command", input.Command);
        AddParameter(command, "@exit_code", input.ExitCode);
        AddParameter(command, "@output_snippet", input.OutputSnippet);
        AddParameter(command, "@passed", input.Passed);
        AddParameter(command, "@ts", timestampUtc);

        object? rawId = command.ExecuteScalar();
        long id = rawId is long value ? value : Convert.ToInt64(rawId);

        return new InsertCheckResult(
            input.Workspace,
            input.TaskId,
            id,
            input.Phase,
            input.CheckName,
            input.Tool,
            input.Passed,
            DatabasePath,
            CurrentSchemaVersion,
            timestampUtc);
    }

    public int CountChecks(CheckQuery query)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        StringBuilder sql = new(
            """
            SELECT COUNT(*)
            FROM anvil_checks
            """);

        AppendFilters(sql, command, query);
        command.CommandText = sql.ToString();

        object? rawCount = command.ExecuteScalar();
        return rawCount switch
        {
            long value => Convert.ToInt32(value),
            int value => value,
            _ => Convert.ToInt32(rawCount)
        };
    }

    public IReadOnlyList<CheckRow> ListChecks(CheckQuery query, bool descending)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();

        StringBuilder sql = new(
            """
            SELECT
                id,
                workspace,
                task_id,
                phase,
                check_name,
                tool,
                command,
                exit_code,
                output_snippet,
                passed,
                ts
            FROM anvil_checks
            """);

        AppendFilters(sql, command, query);
        sql.Append(descending ? " ORDER BY id DESC" : " ORDER BY id ASC");
        sql.Append(" LIMIT @limit");
        AddParameter(command, "@limit", query.Limit);

        command.CommandText = sql.ToString();

        List<CheckRow> items = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new CheckRow(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10)));
        }

        return items;
    }

    internal void DeleteChecks(string workspace, string taskId)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM anvil_checks
            WHERE workspace = @workspace AND task_id = @task_id
            """;
        AddParameter(command, "@workspace", workspace);
        AddParameter(command, "@task_id", taskId);
        command.ExecuteNonQuery();
    }

    public static string NormalizeRequired(string fieldName, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} exceeds the maximum length of {maxLength}.");
        }

        return normalized;
    }

    public static string? NormalizeOptional(string fieldName, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} exceeds the maximum length of {maxLength}.");
        }

        return normalized;
    }

    public static string NormalizePhase(string? phase, bool required)
    {
        if (string.IsNullOrWhiteSpace(phase))
        {
            if (required)
            {
                throw new InvalidOperationException("phase is required.");
            }

            return null!;
        }

        string normalized = phase.Trim().ToLowerInvariant();
        if (!ValidPhases.Contains(normalized))
        {
            throw new InvalidOperationException("phase must be one of: baseline, after, review.");
        }

        return normalized;
    }

    public static int ParsePassedRequired(System.Text.Json.JsonElement passed)
    {
        int? normalized = ParsePassedOptional(passed);
        if (!normalized.HasValue)
        {
            throw new InvalidOperationException("passed is required and must be true, false, 1, or 0.");
        }

        return normalized.Value;
    }

    public static int? ParsePassedOptional(System.Text.Json.JsonElement? passed)
    {
        if (!passed.HasValue)
        {
            return null;
        }

        System.Text.Json.JsonElement value = passed.Value;
        return value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => 1,
            System.Text.Json.JsonValueKind.False => 0,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Undefined => null,
            System.Text.Json.JsonValueKind.Number when value.TryGetInt32(out int number) && (number == 0 || number == 1) => number,
            _ => throw new InvalidOperationException("passed must be a boolean or integer 0/1.")
        };
    }

    public static int NormalizeLimit(int? limit, int fallback)
    {
        int normalized = limit ?? fallback;
        if (normalized < 1 || normalized > MaxLimit)
        {
            throw new InvalidOperationException($"limit must be between 1 and {MaxLimit}.");
        }

        return normalized;
    }

    private void Bootstrap()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS anvil_checks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                workspace TEXT NOT NULL,
                task_id TEXT NOT NULL,
                phase TEXT NOT NULL CHECK (phase IN ('baseline', 'after', 'review')),
                check_name TEXT NOT NULL,
                tool TEXT NOT NULL,
                command TEXT NULL,
                exit_code INTEGER NULL,
                output_snippet TEXT NULL,
                passed INTEGER NOT NULL CHECK (passed IN (0, 1)),
                ts TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_anvil_checks_workspace_task
                ON anvil_checks (workspace, task_id, id DESC);

            CREATE INDEX IF NOT EXISTS ix_anvil_checks_workspace_task_phase
                ON anvil_checks (workspace, task_id, phase, id DESC);
            """;
        command.ExecuteNonQuery();

        using SqliteCommand versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        int currentVersion = Convert.ToInt32(versionCommand.ExecuteScalar() ?? 0);

        if (currentVersion < CurrentSchemaVersion)
        {
            using SqliteCommand setVersionCommand = connection.CreateCommand();
            setVersionCommand.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
            setVersionCommand.ExecuteNonQuery();
        }
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(ConnectionString);
        connection.Open();
        return connection;
    }

    private static void AppendFilters(StringBuilder sql, SqliteCommand command, CheckQuery query)
    {
        sql.Append(
            """
             WHERE workspace = @workspace
               AND task_id = @task_id
            """);

        AddParameter(command, "@workspace", query.Workspace);
        AddParameter(command, "@task_id", query.TaskId);

        if (!string.IsNullOrEmpty(query.Phase))
        {
            sql.Append(" AND phase = @phase");
            AddParameter(command, "@phase", query.Phase);
        }

        if (!string.IsNullOrEmpty(query.CheckName))
        {
            sql.Append(" AND check_name = @check_name");
            AddParameter(command, "@check_name", query.CheckName);
        }

        if (query.Passed.HasValue)
        {
            sql.Append(" AND passed = @passed");
            AddParameter(command, "@passed", query.Passed.Value);
        }
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
