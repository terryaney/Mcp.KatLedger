using System.Text;
using Microsoft.Data.Sqlite;

namespace KatLedger;

internal sealed class KatLedgerStore
{
    public const int DefaultQueryLimit = 50;
    public const int MaxQueryLimit = 200;
    public const int MaxSqlLength = 20000;

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
        using SqliteConnection _ = store.OpenConnection();
        Current = store;
        return store;
    }

    public ExecuteResult Execute(string sql)
    {
        SqlStatement statement = AnalyzeSql(sql, readOnly: false);

        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = statement.Sql;
        command.ExecuteNonQuery();

        int rowsAffected = ReadInt32Scalar(connection, "SELECT changes();");
        long? lastInsertRowId = statement.StatementType is "insert" or "replace"
            ? ReadInt64Scalar(connection, "SELECT last_insert_rowid();")
            : null;

        if (lastInsertRowId <= 0)
        {
            lastInsertRowId = null;
        }

        return new ExecuteResult(statement.StatementType, rowsAffected, lastInsertRowId);
    }

    public QueryResult Query(string sql, int limit)
    {
        int normalizedLimit = NormalizeLimit(limit);
        SqlStatement statement = AnalyzeSql(sql, readOnly: true);
        QueryRows result = ReadRows(statement.Sql, normalizedLimit, probeExtraRow: true);

        return new QueryResult(
            statement.StatementType,
            result.Rows.Count,
            normalizedLimit,
            result.Truncated,
            result.Columns,
            result.Rows);
    }

    public QueryOneResult QueryOne(string sql)
    {
        SqlStatement statement = AnalyzeSql(sql, readOnly: true);
        QueryRows result = ReadRows(statement.Sql, 1, probeExtraRow: true);
        IReadOnlyDictionary<string, object?>? row = result.Rows.Count > 0 ? result.Rows[0] : null;

        return new QueryOneResult(
            statement.StatementType,
            row is not null,
            result.Truncated,
            result.Columns,
            row);
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

    public static int NormalizeLimit(int? limit)
    {
        int normalized = limit ?? DefaultQueryLimit;
        if (normalized < 1 || normalized > MaxQueryLimit)
        {
            throw new InvalidOperationException($"limit must be between 1 and {MaxQueryLimit}.");
        }

        return normalized;
    }

    private QueryRows ReadRows(string sql, int limit, bool probeExtraRow)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM ({sql}) AS kat_ledger_result LIMIT @limit";
        AddParameter(command, "@limit", probeExtraRow ? limit + 1 : limit);

        using SqliteDataReader reader = command.ExecuteReader();

        List<string> columns = [];
        for (int index = 0; index < reader.FieldCount; index++)
        {
            columns.Add(reader.GetName(index));
        }

        List<IReadOnlyDictionary<string, object?>> rows = [];
        while (reader.Read())
        {
            if (rows.Count == limit)
            {
                return new QueryRows(columns, rows, Truncated: true);
            }

            Dictionary<string, object?> row = new(reader.FieldCount, StringComparer.Ordinal);
            for (int index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return new QueryRows(columns, rows, Truncated: false);
    }

    private static SqlStatement AnalyzeSql(string sql, bool readOnly)
    {
        string normalized = NormalizeRequired("sql", sql, MaxSqlLength);
        SqlScanResult scan = ScanSql(normalized);

        if (scan.SemicolonCount > 1)
        {
            throw new InvalidOperationException("sql must contain exactly one statement.");
        }

        if (scan.SemicolonCount == 1)
        {
            if (HasNonTriviaAfterSemicolon(normalized, scan.LastSemicolonIndex + 1))
            {
                throw new InvalidOperationException("sql must contain exactly one statement.");
            }

            normalized = normalized[..scan.LastSemicolonIndex].TrimEnd();
            scan = ScanSql(normalized);
        }

        if (scan.Tokens.Count == 0)
        {
            throw new InvalidOperationException("sql must contain a statement.");
        }

        if (scan.Tokens.Contains("attach") || scan.Tokens.Contains("detach"))
        {
            throw new InvalidOperationException("ATTACH and DETACH are not allowed.");
        }

        string statementType = scan.Tokens[0];
        bool isReadStatement = statementType is "select" or "with";

        if (readOnly && !isReadStatement)
        {
            throw new InvalidOperationException("query and query_one only accept SELECT or WITH statements.");
        }

        if (!readOnly && isReadStatement)
        {
            throw new InvalidOperationException("execute does not accept SELECT or WITH statements.");
        }

        return new SqlStatement(normalized, statementType);
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(ConnectionString);
        connection.Open();
        return connection;
    }

    private static int ReadInt32Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private static long ReadInt64Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
    }

    private static SqlScanResult ScanSql(string sql)
    {
        List<string> tokens = [];
        StringBuilder token = new();
        SqlScanState state = SqlScanState.Normal;
        int semicolonCount = 0;
        int lastSemicolonIndex = -1;

        for (int index = 0; index < sql.Length; index++)
        {
            char character = sql[index];

            switch (state)
            {
                case SqlScanState.Normal:
                    if (character == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.LineComment;
                        index++;
                        continue;
                    }

                    if (character == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.BlockComment;
                        index++;
                        continue;
                    }

                    if (character == '\'')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.SingleQuote;
                        continue;
                    }

                    if (character == '"')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.DoubleQuote;
                        continue;
                    }

                    if (character == '[')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.BracketIdentifier;
                        continue;
                    }

                    if (character == '`')
                    {
                        FlushToken(tokens, token);
                        state = SqlScanState.BacktickIdentifier;
                        continue;
                    }

                    if (character == ';')
                    {
                        FlushToken(tokens, token);
                        semicolonCount++;
                        lastSemicolonIndex = index;
                        continue;
                    }

                    if (char.IsLetterOrDigit(character) || character == '_')
                    {
                        token.Append(char.ToLowerInvariant(character));
                        continue;
                    }

                    FlushToken(tokens, token);
                    continue;

                case SqlScanState.LineComment:
                    if (character is '\r' or '\n')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;

                case SqlScanState.BlockComment:
                    if (character == '*' && index + 1 < sql.Length && sql[index + 1] == '/')
                    {
                        state = SqlScanState.Normal;
                        index++;
                    }

                    continue;

                case SqlScanState.SingleQuote:
                    if (character == '\'' && index + 1 < sql.Length && sql[index + 1] == '\'')
                    {
                        index++;
                        continue;
                    }

                    if (character == '\'')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;

                case SqlScanState.DoubleQuote:
                    if (character == '"' && index + 1 < sql.Length && sql[index + 1] == '"')
                    {
                        index++;
                        continue;
                    }

                    if (character == '"')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;

                case SqlScanState.BracketIdentifier:
                    if (character == ']')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;

                case SqlScanState.BacktickIdentifier:
                    if (character == '`' && index + 1 < sql.Length && sql[index + 1] == '`')
                    {
                        index++;
                        continue;
                    }

                    if (character == '`')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;
            }
        }

        FlushToken(tokens, token);
        return new SqlScanResult(tokens, semicolonCount, lastSemicolonIndex);
    }

    private static bool HasNonTriviaAfterSemicolon(string sql, int startIndex)
    {
        SqlScanState state = SqlScanState.Normal;

        for (int index = startIndex; index < sql.Length; index++)
        {
            char character = sql[index];

            switch (state)
            {
                case SqlScanState.Normal:
                    if (char.IsWhiteSpace(character))
                    {
                        continue;
                    }

                    if (character == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
                    {
                        state = SqlScanState.LineComment;
                        index++;
                        continue;
                    }

                    if (character == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
                    {
                        state = SqlScanState.BlockComment;
                        index++;
                        continue;
                    }

                    return true;

                case SqlScanState.LineComment:
                    if (character is '\r' or '\n')
                    {
                        state = SqlScanState.Normal;
                    }

                    continue;

                case SqlScanState.BlockComment:
                    if (character == '*' && index + 1 < sql.Length && sql[index + 1] == '/')
                    {
                        state = SqlScanState.Normal;
                        index++;
                    }

                    continue;

                default:
                    return true;
            }
        }

        return false;
    }

    private static void FlushToken(List<string> tokens, StringBuilder token)
    {
        if (token.Length == 0)
        {
            return;
        }

        tokens.Add(token.ToString());
        token.Clear();
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private readonly record struct SqlStatement(string Sql, string StatementType);

    private readonly record struct QueryRows(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
        bool Truncated);

    private readonly record struct SqlScanResult(
        IReadOnlyList<string> Tokens,
        int SemicolonCount,
        int LastSemicolonIndex);

    private enum SqlScanState
    {
        Normal,
        SingleQuote,
        DoubleQuote,
        BracketIdentifier,
        BacktickIdentifier,
        LineComment,
        BlockComment
    }
}
