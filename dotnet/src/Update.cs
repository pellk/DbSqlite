using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

/// <summary>Builds parameterized SQLite UPDATE statements.</summary>
public static class UpdateQuery
{
	internal static (string Query, SqliteParameter[] SqlParams) Create(JsonElement json, string table, string primaryKey)
		=> Create(Data.JsonFields(json), table, primaryKey);

	internal static (string Query, SqliteParameter[] SqlParams) Create(JsonObject json, string table, string primaryKey)
		=> Create(Data.JsonFields(json), table, primaryKey);

	private static (string Query, SqliteParameter[] SqlParams) Create(
		IEnumerable<(string Name, object Value)> fields, string table, string primaryKey)
	{
		var values = fields.ToArray();
		int keyIndex = Array.FindIndex(values, field => field.Name == primaryKey);
		if (keyIndex < 0)
			throw new ArgumentException($"The JSON object must contain the primary key '{primaryKey}'.", nameof(fields));

		return Build(values.Where(field => field.Name != primaryKey), table, primaryKey, values[keyIndex].Value);
	}

	internal static (string Query, SqliteParameter[] SqlParams) Build(
		IEnumerable<(string Name, object Value)> fields, string table, string primaryKey, object keyValue)
	{
		string target = Data.EscapeTable(table);
		string key = Data.EscapeIdentifier(primaryKey);
		var assignments = new List<string>();
		var parameters = new List<SqliteParameter>();
		foreach (var (name, value) in fields)
		{
			string parameter = $"@p{parameters.Count}";
			assignments.Add($"{Data.EscapeIdentifier(name)}={parameter}");
			parameters.Add(new SqliteParameter(parameter, Data.ParameterValue(value)));
		}

		if (assignments.Count == 0)
			throw new ArgumentException("At least one field to update is required.", nameof(fields));

		parameters.Add(new SqliteParameter("@id", Data.ParameterValue(keyValue)));
		return ($"UPDATE {target} AS target SET {string.Join(",", assignments)} WHERE target.{key}=@id", parameters.ToArray());
	}
}

public static partial class Db
{
	/// <summary>Updates a row by the primary key in a JSON object and returns the affected-row count.</summary>
	public static int Update(JsonNode json, string table, string primaryKey)
		=> Update(json.AsObject(), table, primaryKey);

	/// <summary>Updates a row by the primary key in a JSON object and returns the affected-row count.</summary>
	public static int Update(JsonObject json, string table, string primaryKey)
		=> Update(UpdateQuery.Create(json, table, primaryKey));

	/// <summary>Updates a row by the primary key in a JSON object and returns the affected-row count.</summary>
	public static int Update(JsonElement json, string table, string primaryKey)
		=> Update(UpdateQuery.Create(json, table, primaryKey));

	private static int Update((string Query, SqliteParameter[] SqlParams) command)
		=> Execute(command.Query, command.SqlParams);
}
