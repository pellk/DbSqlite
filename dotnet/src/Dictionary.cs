using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Executes the query and maps the first two columns of the result set into a <see cref="System.Collections.Generic.Dictionary{K, V}"/>.</summary>
	/// <typeparam name="K">The type of the dictionary keys, read from the first column.</typeparam>
	/// <typeparam name="V">The type of the dictionary values, read from the second column.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>A dictionary of keys and values, or null if no rows were returned.</returns>
	public static Dictionary<K, V> Dictionary<K, V>(string query, params (string name, object value)[] parameters) where K : notnull
		=> Dictionary<K, V>(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and maps the first two columns of the result set into a <see cref="System.Collections.Generic.Dictionary{K, V}"/>.</summary>
	/// <typeparam name="K">The type of the dictionary keys, read from the first column.</typeparam>
	/// <typeparam name="V">The type of the dictionary values, read from the second column.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>A dictionary of keys and values, or null if no rows were returned.</returns>
	public static Dictionary<K, V> Dictionary<K, V>(string query) where K : notnull
		=> Dictionary<K, V>(query, Data.NoParams);

	/// <summary>Executes the query and maps the first two columns of the result set into a <see cref="System.Collections.Generic.Dictionary{K, V}"/>.</summary>
	/// <typeparam name="K">The type of the dictionary keys, read from the first column.</typeparam>
	/// <typeparam name="V">The type of the dictionary values, read from the second column.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>A dictionary of keys and values, or null if no rows were returned.</returns>
	public static Dictionary<K, V> Dictionary<K, V>(string query, params SqliteParameter[] parameters) where K : notnull
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		return ReadDictionary<K, V>(reader);
	}

	internal static Dictionary<K, V> ReadDictionary<K, V>(DbDataReader reader) where K : notnull
	{
		if (!reader.HasRows || reader.FieldCount < 2)
			return null;

		var dictionary = new Dictionary<K, V>();
		while (reader.Read())
		{
			if (reader.IsDBNull(0))
				continue;
			K key = Data.ConvertValue<K>(reader.GetValue(0));
			V value = reader.IsDBNull(1) ? default : Data.ConvertValue<V>(reader.GetValue(1));
			dictionary[key] = value;
		}
		return dictionary;
	}
}
