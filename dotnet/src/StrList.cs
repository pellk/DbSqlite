using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{String}"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{String}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<string> StrList(string query, params (string name, object value)[] parameters)
		=> StrList(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{String}"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{String}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<string> StrList(string query)
		=> StrList(query, Data.NoParams);

	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{String}"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{String}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<string> StrList(string query, params SqliteParameter[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		if (!reader.HasRows || reader.FieldCount == 0)
			return null;
		var list = new List<string>();
		while (reader.Read())
			list.Add(reader.IsDBNull(0) ? null : reader.GetValue(0).ToString());
		return list;
	}
}
