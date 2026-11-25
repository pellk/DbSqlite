using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and maps each row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of objects to create and populate from the result set.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the mapped objects, or null if no rows were returned.</returns>
	public static List<T> ObjList<T>(string query, params (string name, object value)[] parameters) where T : new()
		=> ObjList<T>(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and maps each row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of objects to create and populate from the result set.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the mapped objects, or null if no rows were returned.</returns>
	public static List<T> ObjList<T>(string query) where T : new()
		=> ObjList<T>(query, Data.NoParams);

	/// <summary>Executes the query and maps each row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of objects to create and populate from the result set.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>A <see cref="System.Collections.Generic.List{T}"/> containing the mapped objects, or null if no rows were returned.</returns>
	public static List<T> ObjList<T>(string query, params SqliteParameter[] parameters) where T : new()
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		if (!reader.HasRows)
			return null;

		var list = new List<T>();
		var mappings = reader.GetColumnMappings<T>();
		while (reader.Read())
			list.Add(reader.GetObject(mappings));
		return list;
	}
}
