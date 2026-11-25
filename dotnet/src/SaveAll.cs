using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Deletes, updates, and inserts rows from a JSON array in a single transaction.</summary>
	public static int SaveAll(JsonNode json, string deleteIds, string table, string idProp = "Id")
	{
		ArgumentNullException.ThrowIfNull(json);
		if (json is not JsonArray array)
			throw new ArgumentException("A JSON array is required.", nameof(json));
		return SaveAll(array, deleteIds, table, idProp);
	}

	/// <summary>Deletes, updates, and inserts rows from a JSON array in a single transaction.</summary>
	public static int SaveAll(JsonArray json, string deleteIds, string table, string idProp = "Id")
	{
		ArgumentNullException.ThrowIfNull(json);
		return SaveAll(json.OfType<JsonObject>().Select(row => Data.JsonFields(row)), deleteIds, table, idProp);
	}

	/// <summary>Deletes, updates, and inserts rows from a JSON array in a single transaction.</summary>
	public static int SaveAll(JsonElement json, string deleteIds, string table, string idProp = "Id")
	{
		if (json.ValueKind != JsonValueKind.Array)
			throw new ArgumentException("A JSON array is required.", nameof(json));
		return SaveAll(json.EnumerateArray().Select(row => Data.JsonFields(row)), deleteIds, table, idProp);
	}

	private static int SaveAll(IEnumerable<IEnumerable<(string Name, object Value)>> rows,
		string deleteIds, string table, string idProp)
	{
		string target = Data.EscapeTable(table);
		string key = Data.EscapeIdentifier(idProp);
		var ids = ParseDeleteIds(deleteIds);
		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		int affected = 0;

		foreach (var chunk in ids.Chunk(500))
		{
			var parameters = chunk.Select((id, index) => new SqliteParameter($"@id{index}", id)).ToArray();
			string placeholders = string.Join(",", parameters.Select(parameter => parameter.ParameterName));
			affected += Run($"DELETE FROM {target} AS target WHERE target.{key} IN ({placeholders})", parameters);
		}

		var inserts = new List<Dictionary<string, object>>();
		foreach (var row in rows)
		{
			var fields = row.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);
			fields.Remove(idProp, out var idValue);
			int id = SaveQuery.GetId(idValue);
			if (id == 0)
				inserts.Add(fields);
			else
			{
				var update = UpdateQuery.Build(fields.Select(field => (field.Key, field.Value)), table, idProp, id);
				affected += Run(update.Query, update.SqlParams);
			}
		}

		if (inserts.Count > 0)
		{
			var names = inserts[0].Keys.ToArray();
			var insert = Data.InsertQuery(names.Select(name => (name, (object)null)), table);
			using var command = new SqliteCommand(insert.Query, connection, transaction);
			Data.AttachParams(command, insert.SqlParams);
			foreach (var row in inserts)
			{
				for (int i = 0; i < names.Length; i++)
					insert.SqlParams[i].Value = Data.ParameterValue(row.GetValueOrDefault(names[i]));
				affected += command.ExecuteNonQuery();
			}
		}

		transaction.Commit();
		return affected;

		int Run(string query, SqliteParameter[] parameters)
		{
			using var command = new SqliteCommand(query, connection, transaction);
			Data.AttachParams(command, parameters);
			return command.ExecuteNonQuery();
		}
	}

	private static long[] ParseDeleteIds(string deleteIds)
	{
		if (string.IsNullOrWhiteSpace(deleteIds))
			return [];
		string[] parts = deleteIds.Split(',');
		var ids = new long[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out ids[i]))
				throw new InvalidDataException("Delete IDs must be comma-separated non-negative integers.");
		}
		return ids;
	}
}
