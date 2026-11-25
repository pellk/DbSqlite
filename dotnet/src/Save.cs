using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

internal readonly record struct SaveParams(int Id, string Query, SqliteParameter[] SqlParams);

internal static partial class SaveQuery
{
	internal static SaveParams Create(JsonElement json, string table, string idProp)
		=> Create(Data.JsonFields(json), table, idProp, StringComparison.Ordinal);

	internal static SaveParams Create(JsonObject json, string table, string idProp)
		=> Create(Data.JsonFields(json), table, idProp, StringComparison.Ordinal);

	private static SaveParams Create(IEnumerable<(string Name, object Value)> fields, string table, string idProp,
		StringComparison comparison)
	{
		idProp ??= "Id";
		var values = fields.ToArray();
		int id = GetId(values.FirstOrDefault(field => field.Name.Equals(idProp, comparison)).Value);
		var data = values.Where(field => !field.Name.Equals(idProp, comparison));

		if (id != 0)
		{
			var update = UpdateQuery.Build(data, table, idProp, id);
			return new SaveParams(id, update.Query, update.SqlParams);
		}

		var insert = Data.InsertQuery(data, table);
		return new SaveParams(0, $"{insert.Query} RETURNING {Data.EscapeIdentifier(idProp)}", insert.SqlParams);
	}

	internal static int GetId(object value)
	{
		object parameter = Data.ParameterValue(value);
		return parameter switch
		{
			int id => id,
			long id => checked((int)id),
			byte or sbyte or short or ushort or uint or ulong => Convert.ToInt32(parameter, CultureInfo.InvariantCulture),
			string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) => checked((int)id),
			_ => 0
		};
	}
}

public static partial class Db
{
	/// <summary>Inserts a JSON row with a missing or zero ID, or updates a row by its ID.</summary>
	/// <returns>The inserted or updated ID, or zero if no row was updated.</returns>
	public static int Save(JsonNode json, string table, string idProp = "Id")
		=> Save(json.AsObject(), table, idProp);

	/// <summary>Inserts a JSON row with a missing or zero ID, or updates a row by its ID.</summary>
	/// <returns>The inserted or updated ID, or zero if no row was updated.</returns>
	public static int Save(JsonObject json, string table, string idProp = "Id")
		=> Save(SaveQuery.Create(json, table, idProp));

	/// <summary>Inserts a JSON row with a missing or zero ID, or updates a row by its ID.</summary>
	/// <returns>The inserted or updated ID, or zero if no row was updated.</returns>
	public static int Save(JsonElement json, string table, string idProp = "Id")
		=> Save(SaveQuery.Create(json, table, idProp));

	private static int Save(SaveParams entry)
	{
		if (entry.Id != 0)
			return Execute(entry.Query, entry.SqlParams) > 0 ? entry.Id : 0;

		using var connection = new SqliteConnection(ConnectionString);
		connection.Open();
		using var transaction = connection.BeginTransaction();
		using var command = new SqliteCommand(entry.Query, connection, transaction);
		Data.AttachParams(command, entry.SqlParams);

		int id;
		using (var reader = command.ExecuteReader())
		{
			if (!reader.Read() || reader.GetValue(0) is not long inserted)
				throw new InvalidOperationException("Save requires a generated INTEGER PRIMARY KEY.");

			id = checked((int)inserted);
			while (reader.Read()) { }
		}

		transaction.Commit();
		return id;
	}
}
