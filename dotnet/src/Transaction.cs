using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes a SQL batch in a transaction and returns its affected-row count in an array.</summary>
	public static int[] Transaction(string query) => Transaction(query, Data.NoParams);

	/// <summary>Executes a SQL batch with named parameters in a transaction.</summary>
	public static int[] Transaction(string query, params (string name, object value)[] parameters)
		=> Transaction(query, Data.SqlParams(parameters));

	/// <summary>Executes a SQL batch with SQLite parameters in a transaction.</summary>
	public static int[] Transaction(string query, params SqliteParameter[] parameters)
		=> query == null ? null : Transaction([(query, parameters)]);

	/// <summary>Executes SQL statements in one transaction and returns a row count per statement.</summary>
	public static int[] Transaction(IEnumerable<string> queries)
		=> Transaction(queries?.Select(query => (query, Data.NoParams)));

	/// <summary>Executes SQL commands with SQLite parameters in one transaction.</summary>
	public static int[] Transaction(params (string query, SqliteParameter[] parameters)[] commands)
		=> Transaction((IEnumerable<(string, SqliteParameter[])>)commands);

	/// <summary>Executes SQL commands with SQLite parameters in one transaction.</summary>
	public static int[] Transaction(IEnumerable<(string query, SqliteParameter[] parameters)> commands)
		=> RunTransaction(commands, static (connection, transaction, entry) =>
		{
			using var command = new SqliteCommand(entry.query, connection, transaction);
			Data.AttachParams(command, entry.parameters);
			return Data.ExecuteBatch(command);
		});

	/// <summary>Executes SQL commands with named parameters in one transaction.</summary>
	public static int[] Transaction(params (string query, (string name, object value)[] parameters)[] commands)
		=> Transaction((IEnumerable<(string, (string, object)[])>)commands);

	/// <summary>Executes SQL commands with named parameters in one transaction.</summary>
	public static int[] Transaction(IEnumerable<(string query, (string name, object value)[] parameters)> commands)
		=> Transaction(commands?.Select(command => (command.query, Data.SqlParams(command.parameters))));

	/// <summary>Executes SQLite commands in one transaction.</summary>
	public static int[] Transaction(params SqliteCommand[] commands)
		=> Transaction((IEnumerable<SqliteCommand>)commands);

	/// <summary>Executes SQLite commands in one transaction.</summary>
	public static int[] Transaction(IEnumerable<SqliteCommand> commands)
		=> RunTransaction(commands?.Where(command => command != null), static (connection, transaction, source) =>
		{
			using var command = new SqliteCommand(source.CommandText, connection, transaction)
			{
				CommandTimeout = source.CommandTimeout
			};
			foreach (SqliteParameter parameter in source.Parameters)
				command.Parameters.Add(parameter);
			return Data.ExecuteBatch(command);
		});

	private static int[] RunTransaction<T>(IEnumerable<T> commands, Func<SqliteConnection, SqliteTransaction, T, int> execute)
	{
		if (commands == null)
			return null;

		var counts = commands.TryGetNonEnumeratedCount(out int count) ? new List<int>(count) : new List<int>();
		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		foreach (var command in commands)
			counts.Add(execute(connection, transaction, command));
		transaction.Commit();
		return counts.ToArray();
	}
}
