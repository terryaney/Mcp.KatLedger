namespace KatLedger;

public sealed record ExecuteResult(
    string StatementType,
    int RowsAffected,
    long? LastInsertRowId);

public sealed record QueryResult(
    string StatementType,
    int Returned,
    int Limit,
    bool Truncated,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

public sealed record QueryOneResult(
    string StatementType,
    bool Found,
    bool Multiple,
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, object?>? Row);
