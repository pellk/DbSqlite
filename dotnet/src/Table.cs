using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and returns the results in a <see cref="DataTable"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>A <see cref="DataTable"/> containing the query results, or null if no rows were returned.</returns>
	public static DataTable Table(string query, params (string name, object value)[] parameters)
		=> Table(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and returns the results in a <see cref="DataTable"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>A <see cref="DataTable"/> containing the query results, or null if no rows were returned.</returns>
	public static DataTable Table(string query)
		=> Table(query, Data.NoParams);

	/// <summary>Executes the query and returns the results in a <see cref="DataTable"/>.</summary>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>A <see cref="DataTable"/> containing the query results, or null if no rows were returned.</returns>
	public static DataTable Table(string query, params SqliteParameter[] parameters)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		return ReadTable(reader);
	}

	internal static DataTable ReadTable(DbDataReader reader)
	{
		if (!reader.HasRows || reader.FieldCount == 0)
			return null;

		var dt = new DataTable();
		int columns = reader.FieldCount;
		for (int i = 0; i < columns; i++)
		{
			string name = reader.GetName(i);

			if (string.IsNullOrEmpty(name))
			{
				name = $"Column{i + 1}";
			}
			if (dt.Columns.Contains(name))
			{
				int suffix = 1;
				string uniqueName = $"{name}_{suffix}";
				while (dt.Columns.Contains(uniqueName))
					uniqueName = $"{name}_{++suffix}";
				name = uniqueName;
			}

			dt.Columns.Add(name, typeof(object));
		}

		dt.BeginLoadData();
		try
		{
			var values = new object[columns];
			while (reader.Read())
			{
				reader.GetValues(values);
				dt.Rows.Add(values);
			}
		}
		finally
		{
			dt.EndLoadData();
		}

		return dt;
	}
}
