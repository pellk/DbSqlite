using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

/// <summary>Represents the format or structure to use when outputting JSON data.</summary>
public enum JsonValueType
{
	/// <summary>JSON array format.</summary>
	Array,
	/// <summary>JSON object format.</summary>
	Object,
	/// <summary>Raw scalar value format in a named result.</summary>
	Value,
	/// <summary>Compact CSV string in a named result.</summary>
	Csv
}

/// <summary>Shared helpers for SQLite data access and serialization.</summary>
public static partial class Data
{
	internal static readonly SqliteParameter[] NoParams = [];

	internal static SqliteParameter[] SqlParams((string name, object value)[] parameters)
	{
		if (parameters is not { Length: > 0 })
			return NoParams;

		var result = new SqliteParameter[parameters.Length];
		for (int i = 0; i < parameters.Length; i++)
			result[i] = new SqliteParameter(parameters[i].name, ParameterValue(parameters[i].value));
		return result;
	}

	internal static object ParameterValue(object value) => value switch
	{
		null => DBNull.Value,
		Enum e => Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture),
		JsonElement element => JsonParameterValue(element),
		JsonDocument document => JsonParameterValue(document.RootElement),
		JsonNode node => JsonParameterValue(JsonSerializer.SerializeToElement(node)),
		_ => value
	};

	private static object JsonParameterValue(JsonElement value) => value.ValueKind switch
	{
		JsonValueKind.Null => DBNull.Value,
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.String => value.GetString(),
		JsonValueKind.Number when value.TryGetInt32(out int integer) => integer,
		JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
		JsonValueKind.Number => value.GetDouble(),
		_ => value.GetRawText()
	};

	internal static readonly JsonWriterOptions JsonWriterOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	internal static string EscapeIdentifier(string identifier)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
		if (identifier.Contains('\0'))
			throw new ArgumentException("SQLite identifiers cannot contain NUL characters.", nameof(identifier));
		return "\"" + identifier.Replace("\"", "\"\"") + "\"";
	}

	internal static string EscapeTable(string table)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(table);
		return string.Join(".", table.Split('.').Select(part => EscapeIdentifier(part.Trim())));
	}

	internal static void AttachParams(SqliteCommand command, SqliteParameter[] parameters)
	{
		if (parameters == null)
			return;

		foreach (var parameter in parameters)
		{
			if (parameter != null)
				command.Parameters.Add(parameter);
		}
	}

	internal static int ExecuteBatch(SqliteCommand command)
	{
		using var reader = command.ExecuteReader();
		// Advance explicitly so a failure in a later statement reaches the caller.
		do
		{
			while (reader.Read()) { }
		}
		while (reader.NextResult());
		return reader.RecordsAffected;
	}

	internal static object ConvertValue(object value, Type targetType)
	{
		targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
		if (targetType.IsInstanceOfType(value))
			return value;

		if (targetType.IsEnum)
			return value is string text
				? Enum.Parse(targetType, text, ignoreCase: true)
				: Enum.ToObject(targetType, value);

		if (targetType == typeof(Guid) && value is byte[] bytes)
			return new Guid(bytes);

		if (value is string str)
		{
			if (targetType == typeof(Guid))
				return Guid.Parse(str);
			if (targetType == typeof(DateTimeOffset))
				return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture);
			if (targetType == typeof(TimeSpan))
				return TimeSpan.Parse(str, CultureInfo.InvariantCulture);
			if (targetType == typeof(DateOnly))
				return DateOnly.Parse(str, CultureInfo.InvariantCulture);
			if (targetType == typeof(TimeOnly))
				return TimeOnly.Parse(str, CultureInfo.InvariantCulture);
		}

		return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
	}

	internal static T ConvertValue<T>(object value) => (T)ConvertValue(value, typeof(T));
}

/// <summary>SQLite query, persistence, and export helpers.</summary>
public static partial class Db
{
	/// <summary>Gets or sets the SQLite connection string used by the helpers.</summary>
	public static string ConnectionString { get; set; }

	/// <summary>Creates a parameter that contains a trimmed string or SQL NULL for blank input.</summary>
	public static SqliteParameter NS(string name, string value)
		=> new(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
}
