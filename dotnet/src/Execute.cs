using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes SQL and returns the number of rows affected.</summary>
	public static int Execute(string query) => Execute(query, Data.NoParams);

	/// <summary>Executes SQL with named parameters and returns the number of rows affected.</summary>
	public static int Execute(string query, params (string name, object value)[] parameters)
		=> Execute(query, Data.SqlParams(parameters));

	/// <summary>Executes SQL with SQLite parameters and returns the number of rows affected.</summary>
	public static int Execute(string query, params SqliteParameter[] parameters)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		Data.AttachParams(command, parameters);
		connection.Open();
		return Data.ExecuteBatch(command);
	}
}
