using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace nuel.Sqlite;

public static partial class Data
{
	internal static object NormalizeJsonValue(object value) => value switch
	{
		DBNull => null,
		double number when !double.IsFinite(number) => number.ToString(CultureInfo.InvariantCulture),
		_ => value
	};

	internal static void WriteDbValue(this Utf8JsonWriter writer, DbDataReader reader, int ordinal)
	{
		switch (NormalizeJsonValue(reader.GetValue(ordinal)))
		{
			case null:
				writer.WriteNullValue();
				break;
			case long integer:
				writer.WriteNumberValue(integer);
				break;
			case double number:
				writer.WriteNumberValue(number);
				break;
			case string text:
				writer.WriteStringValue(text);
				break;
			case byte[] bytes:
				writer.WriteBase64StringValue(bytes);
				break;
			case object value:
				JsonSerializer.Serialize(writer, value, value.GetType());
				break;
		}
	}
}

public static partial class Db
{
	internal static IEnumerable<int> WriteJsonRows(DbDataReader reader, JsonValueType result,
		Utf8JsonWriter writer, CancellationToken cancellationToken = default)
	{
		if (result is not (JsonValueType.Array or JsonValueType.Object))
			throw new ArgumentException("The only valid single-result JSON shapes are array and object.", nameof(result));

		bool array = result == JsonValueType.Array;
		if (array)
			writer.WriteStartArray();

		cancellationToken.ThrowIfCancellationRequested();
		if (reader.Read())
		{
			var names = new JsonEncodedText[reader.FieldCount];
			for (int i = 0; i < names.Length; i++)
				names[i] = JsonEncodedText.Encode(reader.GetName(i), Data.JsonWriterOptions.Encoder);

			do
			{
				writer.WriteStartObject();
				for (int i = 0; i < names.Length; i++)
				{
					writer.WritePropertyName(names[i]);
					writer.WriteDbValue(reader, i);
				}
				writer.WriteEndObject();
				yield return writer.BytesPending;
				cancellationToken.ThrowIfCancellationRequested();
			}
			while (array && reader.Read());
		}
		else if (!array)
		{
			writer.WriteNullValue();
		}

		if (array)
			writer.WriteEndArray();
	}

	internal static IEnumerable<int> WriteJsonResults(DbDataReader reader, (string Name, JsonValueType ResultType)[] results,
		Utf8JsonWriter writer, CancellationToken cancellationToken = default)
	{
		writer.WriteStartObject();
		bool hasResult = true;
		foreach (var (name, type) in results)
		{
			cancellationToken.ThrowIfCancellationRequested();
			writer.WritePropertyName(name);
			if (!hasResult || !reader.HasRows)
			{
				writer.WriteNullValue();
			}
			else
			{
				switch (type)
				{
					case JsonValueType.Value:
						if (reader.Read())
							writer.WriteDbValue(reader, 0);
						else
							writer.WriteNullValue();
						break;
					case JsonValueType.Csv:
						writer.WriteStringValue(reader.ReadCsv(cancellationToken));
						break;
					case JsonValueType.Array:
					case JsonValueType.Object:
						foreach (int pendingBytes in WriteJsonRows(reader, type, writer, cancellationToken))
							yield return pendingBytes;
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(results), type, "Unknown JSON result shape.");
				}
			}
			yield return writer.BytesPending;

			cancellationToken.ThrowIfCancellationRequested();
			hasResult = hasResult && reader.NextResult();
		}
		writer.WriteEndObject();
	}
}
