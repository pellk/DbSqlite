using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Data
{
	internal static (string Query, SqliteParameter[] SqlParams) InsertQuery(JsonElement json, string table)
		=> InsertQuery(JsonFields(json), table);

	internal static (string Query, SqliteParameter[] SqlParams) InsertQuery(JsonObject json, string table)
		=> InsertQuery(JsonFields(json), table);

	internal static (string Query, SqliteParameter[] SqlParams) InsertQuery(
		IEnumerable<(string Name, object Value)> fields, string table)
	{
		string target = EscapeTable(table);
		var columns = new List<string>();
		var parameters = new List<SqliteParameter>();
		foreach (var (name, value) in fields)
		{
			columns.Add(EscapeIdentifier(name));
			parameters.Add(new SqliteParameter($"@p{parameters.Count}", ParameterValue(value)));
		}

		if (columns.Count == 0)
			return ($"INSERT INTO {target} DEFAULT VALUES", NoParams);

		string columnList = string.Join(",", columns);
		string values = string.Join(",", parameters.Select(parameter => parameter.ParameterName));
		return ($"INSERT INTO {target} ({columnList}) VALUES ({values})", parameters.ToArray());
	}

	internal static IEnumerable<(string Name, object Value)> JsonFields(JsonElement json)
	{
		if (json.ValueKind != JsonValueKind.Object)
			throw new ArgumentException("A JSON object is required.", nameof(json));

		foreach (var field in json.EnumerateObject())
			yield return (field.Name, field.Value);
	}

	internal static IEnumerable<(string Name, object Value)> JsonFields(JsonObject json)
	{
		ArgumentNullException.ThrowIfNull(json);
		foreach (var field in json)
			yield return (field.Key, field.Value);
	}
}

public static partial class Db
{
	/// <summary>Inserts a JSON object and returns the number of affected rows.</summary>
	public static int Insert(JsonNode json, string table) => Insert(json.AsObject(), table);

	/// <summary>Inserts a JSON object and returns the number of affected rows.</summary>
	public static int Insert(JsonObject json, string table) => Insert(Data.InsertQuery(json, table));

	/// <summary>Inserts a JSON object and returns the number of affected rows.</summary>
	public static int Insert(JsonElement json, string table) => Insert(Data.InsertQuery(json, table));

	private static int Insert((string Query, SqliteParameter[] SqlParams) command)
		=> Execute(command.Query, command.SqlParams);
}
