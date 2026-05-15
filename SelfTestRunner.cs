using ModelContextProtocol;

namespace KatLedger;

internal static class SelfTestRunner
{
    public static void Run(KatLedgerStore store)
    {
        string tableName = $"katledger_selftest_{Guid.NewGuid():N}";

        try
        {
            ExecuteResult create = KatLedgerTools.Execute(
                $"CREATE TABLE \"{tableName}\" (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, value INTEGER NOT NULL)");

            if (create.StatementType != "create")
            {
                throw new InvalidOperationException("Create self-test failed.");
            }

            ExecuteResult alpha = KatLedgerTools.Execute(
                $"INSERT INTO \"{tableName}\" (name, value) VALUES ('alpha', 1)");

            _ = KatLedgerTools.Execute(
                $"INSERT INTO \"{tableName}\" (name, value) VALUES ('beta', 2)");

            _ = KatLedgerTools.Execute(
                $"INSERT INTO \"{tableName}\" (name, value) VALUES ('gamma', 3)");

            if (!alpha.LastInsertRowId.HasValue || alpha.RowsAffected != 1)
            {
                throw new InvalidOperationException("Insert self-test failed.");
            }

            QueryResult capped = KatLedgerTools.Query(
                $"SELECT id, name, value FROM \"{tableName}\" ORDER BY id ASC",
                limit: 2);

            if (capped.Returned != 2 || !capped.Truncated || capped.Columns.Count != 3)
            {
                throw new InvalidOperationException("Query cap self-test failed.");
            }

            if (!Equals(capped.Rows[0]["name"], "alpha") || Convert.ToInt64(capped.Rows[1]["value"]) != 2L)
            {
                throw new InvalidOperationException("Query payload self-test failed.");
            }

            QueryOneResult single = KatLedgerTools.QueryOne(
                $"SELECT id, name, value FROM \"{tableName}\" WHERE name = 'beta'");

            if (!single.Found || single.Multiple || !Equals(single.Row?["name"], "beta"))
            {
                throw new InvalidOperationException("QueryOne exact-match self-test failed.");
            }

            QueryOneResult first = KatLedgerTools.QueryOne(
                $"SELECT id, name FROM \"{tableName}\" ORDER BY id ASC");

            if (!first.Found || !first.Multiple || !Equals(first.Row?["name"], "alpha"))
            {
                throw new InvalidOperationException("QueryOne multi-row self-test failed.");
            }

            AssertRejected(() => KatLedgerTools.Execute("SELECT 1"), "execute read validation");
            AssertRejected(() => KatLedgerTools.Query($"INSERT INTO \"{tableName}\" (name, value) VALUES ('delta', 4)"), "query write validation");
            AssertRejected(() => KatLedgerTools.Query("ATTACH DATABASE 'other.db' AS other"), "attach validation");
            AssertRejected(() => KatLedgerTools.Query("SELECT 1; SELECT 2"), "multi-statement validation");
        }
        finally
        {
            try
            {
                store.Execute($"DROP TABLE IF EXISTS \"{tableName}\"");
            }
            catch
            {
            }
        }
    }

    private static void AssertRejected(Action action, string scenario)
    {
        try
        {
            action();
            throw new InvalidOperationException($"{scenario} self-test failed.");
        }
        catch (McpException)
        {
        }
    }
}
