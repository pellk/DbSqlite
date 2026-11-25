using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

/// <summary>Writes the compact CSV payload understood by @nuell/db.</summary>
public static partial class CsvWriter
{
	internal static IEnumerable<string> ReadCsvParts(DbDataReader reader, CancellationToken cancellationToken = default)
	{
		if (!reader.HasRows)
			return [];

		string[] names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
		return FormatRows(names, ReadRows());

		IEnumerable<object[]> ReadRows()
		{
			while (true)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!reader.Read())
					yield break;

				var values = new object[names.Length];
				reader.GetValues(values);
				yield return values;
			}
		}
	}

	internal static IEnumerable<string> FormatRows(string[] names, IEnumerable<object[]> rows)
	{
		using var enumerator = rows.GetEnumerator();
		if (!enumerator.MoveNext())
			yield break;

		// A fixed header needs a type for each column. Retain the leading rows until
		// every column has a non-null value or there are no more rows.
		var buffered = new List<object[]>();
		var markers = new char[names.Length];
		int unknown = markers.Length;
		bool hasRow = true;
		do
		{
			object[] row = enumerator.Current;
			buffered.Add(row);
			for (int i = 0; i < markers.Length; i++)
			{
				if (markers[i] == '\0' && row[i] is not null and not DBNull)
				{
					markers[i] = GetMarker(row[i].GetType());
					unknown--;
				}
			}

			if (unknown > 0)
				hasRow = enumerator.MoveNext();
		}
		while (unknown > 0 && hasRow);

		var text = new StringBuilder();
		for (int i = 0; i < markers.Length; i++)
		{
			if (i > 0)
				text.Append('~');
			if (markers[i] == '\0')
				markers[i] = '$';
			text.Append(markers[i]).Append(names[i]);
		}

		yield return text.ToString();
		foreach (var row in buffered)
			yield return FormatRow(row);
		buffered.Clear();

		while (hasRow && enumerator.MoveNext())
			yield return FormatRow(enumerator.Current);

		string FormatRow(object[] row)
		{
			text.Clear().Append('|');
			for (int i = 0; i < markers.Length; i++)
			{
				if (i > 0)
					text.Append('~');

				object value = row[i];
				if (value is null or DBNull)
				{
					text.Append('Ø');
					continue;
				}

				if (GetMarker(value.GetType()) != markers[i])
					throw new InvalidOperationException(
						$"CSV column '{names[i]}' contains mixed value types. Cast the column to one type or export JSON.");

				text.Append(FormatValue(value));
			}
			return text.ToString();
		}
	}

	internal static char GetMarker(Type type) => Type.GetTypeCode(type) switch
	{
		TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
			or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => '!',
		TypeCode.Single or TypeCode.Double or TypeCode.Decimal => '%',
		TypeCode.String or TypeCode.Char or TypeCode.DateTime => '$',
		_ when type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly)
			|| type == typeof(Guid) || type == typeof(TimeSpan) || type == typeof(byte[]) => '$',
		_ => throw new NotSupportedException($"Type '{type.FullName}' is not supported in compact CSV.")
	};

	private static string FormatValue(object value) => value switch
	{
		bool flag => flag ? "1" : "0",
		byte[] bytes => Convert.ToBase64String(bytes),
		DateTime date => date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
		DateTimeOffset date => date.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
		DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
		TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
		Enum item => item.ToString("D"),
		IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString()
	};
}

public static partial class Db
{
	/// <summary>Converts an array of objects with the same properties to compact CSV.</summary>
	/// <returns>The CSV string, or null if the input array is null or empty.</returns>
	public static string Csv(object[] objects)
	{
		if (objects is not { Length: > 0 })
			return null;

		var properties = ObjectSaveCache.GetProperties(objects[0].GetType());
		string[] names = properties.Select(property => property.Name).ToArray();
		var rows = objects.Select(item => properties.Select(property => property.Getter(item)).ToArray());
		return string.Concat(CsvWriter.FormatRows(names, rows));
	}

	/// <summary>Returns the first query result set as compact CSV, or null if it has no rows.</summary>
	public static string Csv(string query) => Csv(query, Data.NoParams);

	/// <summary>Runs a query with named parameters and returns compact CSV.</summary>
	public static string Csv(string query, params (string name, object value)[] parameters)
		=> Csv(query, Data.SqlParams(parameters));

	/// <summary>Runs a query with SQLite parameters and returns compact CSV.</summary>
	public static string Csv(string query, params SqliteParameter[] parameters)
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var command = new SqliteCommand(query, connection);
		Data.AttachParams(command, parameters);
		connection.Open();
		using var reader = command.ExecuteReader();
		return reader.ReadCsv();
	}

	internal static string ReadCsv(this DbDataReader reader, CancellationToken cancellationToken = default)
		=> reader.HasRows ? string.Concat(CsvWriter.ReadCsvParts(reader, cancellationToken)) : null;
}
