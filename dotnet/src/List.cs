using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{T}"/>.</summary>
	/// <typeparam name="T">The value type of the items in the list.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<T> List<T>(string query, params (string name, object value)[] parameters) where T : struct
		=> List<T>(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{T}"/>.</summary>
	/// <typeparam name="T">The value type of the items in the list.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<T> List<T>(string query) where T : struct
		=> List<T>(query, Data.NoParams);

	/// <summary>Executes the query and returns the values of the first column as a <see cref="System.Collections.Generic.List{T}"/>.</summary>
	/// <typeparam name="T">The value type of the items in the list.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the values of the first column, or null if no rows were returned.</returns>
	public static List<T> List<T>(string query, params SqliteParameter[] parameters) where T : struct
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		if (!reader.HasRows || reader.FieldCount == 0)
			return null;

		var list = new List<T>();
		while (reader.Read())
			list.Add(reader.IsDBNull(0) ? default : Data.ConvertValue<T>(reader.GetValue(0)));
		return list;
	}
}
