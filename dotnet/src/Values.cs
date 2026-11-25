using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns all values from all rows and columns across all result sets as an array of objects.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>An array of objects containing all values returned by the query.</returns>
	public static object[] Values(string query, params (string name, object value)[] parameters)
		=> Values(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns all values from all rows and columns across all result sets as an array of objects.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>An array of objects containing all values returned by the query.</returns>
	public static object[] Values(string query)
		=> Values(query, Data.NoParams);

	/// <summary>Executes the query and returns all values from all rows and columns across all result sets as an array of objects.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>An array of objects containing all values returned by the query.</returns>
	public static object[] Values(string query, params SqliteParameter[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		var results = new List<object>();
		AddValues();
		while (reader.NextResult())
			AddValues();
		return results.Count == 0 ? [] : results.ToArray();

		void AddValues()
		{
			int fieldCount = reader.FieldCount;
			if (fieldCount == 0)
				return;

			if (fieldCount == 1)
			{
				while (reader.Read())
					results.Add(reader.GetValue(0));
			}
			else
			{
				var values = new object[fieldCount];
				while (reader.Read())
				{
					reader.GetValues(values);
					for (int i = 0; i < fieldCount; i++)
						results.Add(values[i]);
				}
			}
		}
	}
}
