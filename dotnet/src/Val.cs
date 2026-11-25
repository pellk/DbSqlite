using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns the value of the first column of the first row converted to the value type <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The value type of the returned scalar value.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>The scalar value of type <typeparamref name="T"/>, or default(<typeparamref name="T"/>) if null or DBNull.</returns>
	public static T Val<T>(string query, params (string name, object value)[] parameters) where T : struct
		=> Val<T>(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns the value of the first column of the first row converted to the value type <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The value type of the returned scalar value.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>The scalar value of type <typeparamref name="T"/>, or default(<typeparamref name="T"/>) if null or DBNull.</returns>
	public static T Val<T>(string query) where T : struct
		=> Val<T>(query, Data.NoParams);

	/// <summary>Executes the query and returns the value of the first column of the first row converted to the value type <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The value type of the returned scalar value.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>The scalar value of type <typeparamref name="T"/>, or default(<typeparamref name="T"/>) if null or DBNull.</returns>
	public static T Val<T>(string query, params SqliteParameter[] parameters) where T : struct
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		return reader.Read() && !reader.IsDBNull(0)
			? Data.ConvertValue<T>(reader.GetValue(0))
			: default;
	}
}
