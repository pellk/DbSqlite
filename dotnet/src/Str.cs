using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns the first column of the first row as a <see cref="string"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>The string representation of the first column of the first row, or null if the value is null or DBNull.</returns>
	public static string Str(string query, params (string name, object value)[] parameters)
		=> Str(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns the first column of the first row as a <see cref="string"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>The string representation of the first column of the first row, or null if the value is null or DBNull.</returns>
	public static string Str(string query)
		=> Str(query, Data.NoParams);

	/// <summary>Executes the query and returns the first column of the first row as a <see cref="string"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>The string representation of the first column of the first row, or null if the value is null or DBNull.</returns>
	public static string Str(string query, params SqliteParameter[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		if (reader.Read() && reader.FieldCount > 0 && !reader.IsDBNull(0))
		{
			return reader.GetFieldType(0) == typeof(string)
				? reader.GetString(0)
				: reader.GetValue(0).ToString();
		}
		return null;
	}
}
