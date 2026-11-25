using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace nuel.Sqlite;

internal readonly struct ColumnMapping<T>
{
	public readonly int Ordinal;
	public readonly Action<T, DbDataReader, int> Reader;

	public ColumnMapping(int ordinal, Action<T, DbDataReader, int> reader)
	{
		Ordinal = ordinal;
		Reader = reader;
	}
}

internal static class TypeCache<T>
{
	private static readonly Dictionary<string, Action<T, DbDataReader, int>> _columnReaders;
	private static readonly Dictionary<string, PropertyInfo> _properties;

	static TypeCache()
	{
		var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
		_columnReaders = new Dictionary<string, Action<T, DbDataReader, int>>(props.Length, StringComparer.OrdinalIgnoreCase);
		_properties = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

		foreach (var prop in props)
		{
			if (!prop.CanWrite || prop.GetSetMethod() == null || prop.GetIndexParameters().Length != 0)
				continue;

			_properties.TryAdd(prop.Name, prop);
			if (!typeof(T).IsValueType)
				_columnReaders.TryAdd(prop.Name, CreateColumnReader(prop));
		}
	}

	public static Action<T, DbDataReader, int> GetColumnReader(string columnName)
	{
		_columnReaders.TryGetValue(columnName, out var reader);
		return reader;
	}

	public static Dictionary<string, PropertyInfo> Properties => _properties;

	private static Action<T, DbDataReader, int> CreateColumnReader(PropertyInfo prop)
	{
		Type propType = prop.PropertyType;
		bool acceptsNull = !propType.IsValueType || Nullable.GetUnderlyingType(propType) != null;
		var target = Expression.Parameter(typeof(T), "target");
		var value = Expression.Parameter(typeof(object), "value");
		var assign = Expression.Assign(Expression.Property(target, prop), Expression.Convert(value, propType));
		var setter = Expression.Lambda<Action<T, object>>(assign, target, value).Compile();

		return (item, reader, ordinal) =>
		{
			object cell = reader.GetValue(ordinal);
			if (cell is not DBNull)
				setter(item, Data.ConvertValue(cell, propType));
			else if (acceptsNull)
				setter(item, null);
		};
	}

}

internal static class ObjectReflector
{
	internal static ColumnMapping<T>[] GetColumnMappings<T>(this DbDataReader reader) where T : new()
	{
		int fieldCount = reader.FieldCount;
		var list = new List<ColumnMapping<T>>(fieldCount);
		for (int i = 0; i < fieldCount; i++)
		{
			var colReader = TypeCache<T>.GetColumnReader(reader.GetName(i));
			if (colReader != null)
				list.Add(new ColumnMapping<T>(i, colReader));
		}
		return list.ToArray();
	}

	internal static T GetObject<T>(this DbDataReader reader, ColumnMapping<T>[] mappings) where T : new()
	{
		if (typeof(T).IsValueType)
			return reader.GetObject<T>(TypeCache<T>.Properties);

		var obj = new T();
		for (int i = 0; i < mappings.Length; i++)
			mappings[i].Reader(obj, reader, mappings[i].Ordinal);
		return obj;
	}

	internal static T GetObject<T>(this DbDataReader reader) where T : new()
	{
		var mappings = reader.GetColumnMappings<T>();
		return reader.GetObject(mappings);
	}

	internal static T GetObject<T>(this DbDataReader reader, Dictionary<string, PropertyInfo> props) where T : new()
	{
		int fieldCount = reader.FieldCount;
		object obj = new T();
		for (int i = 0; i < fieldCount; i++)
		{
			if (props.TryGetValue(reader.GetName(i), out var prop) && prop.CanWrite)
			{
				if (reader.IsDBNull(i))
				{
					if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
						prop.SetValue(obj, null);
				}
				else
				{
					prop.SetValue(obj, Data.ConvertValue(reader.GetValue(i), prop.PropertyType));
				}
			}
		}
		return (T)obj;
	}
}

public static partial class Db
{
	/// <summary>Executes the query and maps the first row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of object to create and populate from the result set row.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The parameters for the SQL query.</param>
	/// <returns>An instance of <typeparamref name="T"/> populated from the first row, or default if no rows were returned.</returns>
	public static T Object<T>(string query, params (string name, object value)[] parameters) where T : new()
		=> Object<T>(query, Data.SqlParams(parameters));

	/// <summary>Executes the query and maps the first row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of object to create and populate from the result set row.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <returns>An instance of <typeparamref name="T"/> populated from the first row, or default if no rows were returned.</returns>
	public static T Object<T>(string query) where T : new()
		=> Object<T>(query, Data.NoParams);

	/// <summary>Executes the query and maps the first row of the result set to a new instance of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of object to create and populate from the result set row.</typeparam>
	/// <param name="query">The SQLite SQL text to execute.</param>
	/// <param name="parameters">The SQL parameters to apply to the command.</param>
	/// <returns>An instance of <typeparamref name="T"/> populated from the first row, or default if no rows were returned.</returns>
	public static T Object<T>(string query, params SqliteParameter[] parameters) where T : new()
	{
		using var connection = new SqliteConnection(ConnectionString);
		using var cmd = new SqliteCommand(query, connection);
		Data.AttachParams(cmd, parameters);
		connection.Open();
		using var reader = cmd.ExecuteReader();
		if (reader.Read())
			return reader.GetObject<T>();
		return default;
	}
}
