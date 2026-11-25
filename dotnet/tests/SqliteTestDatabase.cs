using Microsoft.Data.Sqlite;
using SqliteDb = nuel.Sqlite.Db;

namespace Db.Tests;

internal sealed class SqliteTestDatabase : IDisposable
{
	private readonly string? _previousConnectionString = SqliteDb.ConnectionString;
	private readonly string _path = Path.Combine(Path.GetTempPath(), $"nuel-{Guid.NewGuid():N}.db");

	public SqliteTestDatabase()
	{
		SqliteDb.ConnectionString = new SqliteConnectionStringBuilder
		{
			DataSource = _path,
			Pooling = false,
			ForeignKeys = true
		}.ToString();
	}

	public void Dispose()
	{
		SqliteDb.ConnectionString = _previousConnectionString;
		File.Delete(_path);
	}
}
