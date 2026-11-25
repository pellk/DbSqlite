using System.Buffers;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

public static partial class Db
{
	/// <summary>Serializes the first row of a query result as JSON.</summary>
	public static string Json(string query) => Json(query, JsonValueType.Object, Data.NoParams);

	/// <summary>Serializes the first row of a query result as JSON using named parameters.</summary>
	public static string Json(string query, params (string name, object value)[] parameters)
		=> Json(query, JsonValueType.Object, Data.SqlParams(parameters));

	/// <summary>Serializes the first row of a query result as JSON using SQLite parameters.</summary>
	public static string Json(string query, params SqliteParameter[] parameters)
		=> Json(query, JsonValueType.Object, parameters);

	/// <summary>Serializes the first result set as a JSON object or array.</summary>
	public static string Json(string query, JsonValueType result)
		=> Json(query, result, Data.NoParams);

	/// <summary>Serializes the first result set as a JSON object or array using named parameters.</summary>
	public static string Json(string query, JsonValueType result, params (string name, object value)[] parameters)
		=> Json(query, result, Data.SqlParams(parameters));

	/// <summary>Serializes the first result set as a JSON object or array using SQLite parameters.</summary>
	public static string Json(string query, JsonValueType result, params SqliteParameter[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		Data.AttachParams(command, parameters);
		connection.Open();
		using var reader = command.ExecuteReader();
		var buffer = new ArrayBufferWriter<byte>();
		using var writer = new Utf8JsonWriter(buffer, Data.JsonWriterOptions);
		return FinishJson(writer, buffer, WriteJsonRows(reader, result, writer));
	}

	/// <summary>Serializes each result set of a SQLite batch to a named JSON property.</summary>
	public static string Json(string query, (string Name, JsonValueType ResultType)[] result)
		=> Json(query, result, Data.NoParams);

	/// <summary>Serializes the named result sets of a batch using named parameters.</summary>
	public static string Json(string query, (string Name, JsonValueType ResultType)[] result,
		params (string name, object value)[] parameters)
		=> Json(query, result, Data.SqlParams(parameters));

	/// <summary>Serializes the named result sets of a batch using SQLite parameters.</summary>
	public static string Json(string query, (string Name, JsonValueType ResultType)[] result,
		params SqliteParameter[] parameters)
	{
		ArgumentNullException.ThrowIfNull(result);
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		Data.AttachParams(command, parameters);
		connection.Open();
		using var reader = command.ExecuteReader();
		return reader.ReadJson(result);
	}

	internal static string ReadJson(this DbDataReader reader, (string Name, JsonValueType ResultType)[] result)
	{
		var buffer = new ArrayBufferWriter<byte>();
		using var writer = new Utf8JsonWriter(buffer, Data.JsonWriterOptions);
		return FinishJson(writer, buffer, WriteJsonResults(reader, result, writer));
	}

	private static string FinishJson(Utf8JsonWriter writer, ArrayBufferWriter<byte> buffer, IEnumerable<int> writes)
	{
		foreach (int _ in writes) { }
		writer.Flush();
		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}
}
