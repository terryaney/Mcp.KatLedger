<Query Kind="Statements">
  <NuGetReference>Microsoft.Data.Sqlite</NuGetReference>
  <Namespace>KAT.Camelot.Domain</Namespace>
  <Namespace>Microsoft.Data.Sqlite</Namespace>
  <Namespace>Microsoft.EntityFrameworkCore</Namespace>
</Query>

var dbPath = @$"{UserUtil.UserDirectory}\.kat\KatLedger\KatLedger.db";
int? taskId = null; // Set to task Id if you want to see ledger

if (!File.Exists(dbPath))
{
	$"Database not found: {dbPath}".Dump();
	return;
}

// Open read-write so you can apply the requested update
var csb = new SqliteConnectionStringBuilder
{
	DataSource = dbPath,
	Mode = SqliteOpenMode.ReadWrite
};

using var conn = new SqliteConnection(csb.ToString());
conn.Open();

/*
var rowsUpdated = Exec(conn,
	"""
	UPDATE "anvil_checks"
	SET "workspace" = 'c:\BTR\Camelot\Websites\ESS\Nexgen';
	"""
);
$"Updated {rowsUpdated} anvil_checks rows.".Dump();
*/

Sql(conn, 
	"""
	SELECT min(id) as TaskId, workspace as Workspace, task_id as TaskSlug, min(ts) AS DateStart, count(*) AS LedgerEntries
	FROM "anvil_checks"
	GROUP BY workspace, task_id
	ORDER BY DateStart DESC
	LIMIT 20;
	"""
).Dump($"Last 20 Anvil Sessions");

if (taskId is not null)
{
	var ledger = Sql(conn,
		$"""
		SELECT c.*
		FROM "anvil_checks" AS c
		INNER JOIN (
			SELECT workspace, task_id
			FROM "anvil_checks"
			WHERE id = {taskId.Value}
			LIMIT 1
		) AS t
			ON c.workspace = t.workspace
		   AND c.task_id = t.task_id
		ORDER BY c.ts DESC;
		"""
	);
	ledger.Dump($"Ledger for task containing row id {taskId}");
}

var objects = Sql(conn, 
	"""
    SELECT type, name, tbl_name, sql
    FROM sqlite_master
    WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%'
    ORDER BY type, name;
    """
);

objects.Dump("Tables / Views");
// Sql(conn, $"PRAGMA table_info(\"anvil_checks\");").Dump($"Columns in anvil_checks");

// 3) Optional: run any custom SQL
var customSql = "";   // e.g. SELECT COUNT(*) AS RowCount FROM Transactions;

if (!string.IsNullOrWhiteSpace(customSql))
{
	Sql(conn, customSql).Dump("Custom SQL");
}

static DataTable Sql(SqliteConnection conn, string sql)
{
	using var cmd = conn.CreateCommand();
	cmd.CommandText = sql;

	using var reader = cmd.ExecuteReader();
	var table = new DataTable();

	// SQLite is dynamically typed, so a given result column can contain different
	// CLR types across rows. DataTable.Load() infers a fixed column type up front,
	// which can blow up on PRAGMA queries such as table_info when dflt_value varies.
	for (var i = 0; i < reader.FieldCount; i++)
		table.Columns.Add(reader.GetName(i), typeof(object));

	while (reader.Read())
		table.Rows.Add(
			Enumerable.Range(0, reader.FieldCount)
				.Select(i => reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i))
				.ToArray());

	return table;
}

static int Exec(SqliteConnection conn, string sql)
{
	using var cmd = conn.CreateCommand();
	cmd.CommandText = sql;

	return cmd.ExecuteNonQuery();
}

static string QuoteIdent(string name) =>
	"\"" + name.Replace("\"", "\"\"") + "\"";