using System.Data.Common;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Data
{
	internal static JsonObject GetJsonObject(this DbDataReader reader)
	{
		var result = new JsonObject();
		for (int i = 0; i < reader.FieldCount; i++)
		{
			object value = NormalizeJsonValue(reader.GetValue(i));
			result[reader.GetName(i)] = value switch
			{
				null => null,
				byte[] bytes => JsonValue.Create(Convert.ToBase64String(bytes)),
				_ => JsonValue.Create(value)
			};
		}
		return result;
	}
}

public static partial class Db
{
	/// <summary>Returns the first query row as a JSON object or null if no rows were returned.</summary>
	public static JsonObject JsonObject(string query) => JsonObject(query, Data.NoParams);

	/// <summary>Returns the first query row as a JSON object using named parameters.</summary>
	public static JsonObject JsonObject(string query, params (string name, object value)[] parameters)
		=> JsonObject(query, Data.SqlParams(parameters));

	/// <summary>Returns the first query row as a JSON object using SQLite parameters.</summary>
	public static JsonObject JsonObject(string query, params SqliteParameter[] parameters)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		Data.AttachParams(command, parameters);
		connection.Open();
		using var reader = command.ExecuteReader();
		return reader.Read() ? reader.GetJsonObject() : null;
	}
}
