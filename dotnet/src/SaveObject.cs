using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace nuel.Sqlite;

internal sealed class ObjectPropertyAccessor
{
	public readonly string Name;
	public readonly Func<object, object> Getter;

	public ObjectPropertyAccessor(PropertyInfo prop)
	{
		Name = prop.Name;
		Getter = CreateGetter(prop);
	}

	private static Func<object, object> CreateGetter(PropertyInfo prop)
	{
		var param = Expression.Parameter(typeof(object), "obj");
		Type declaringType = prop.DeclaringType ?? typeof(object);
		Expression instance = declaringType.IsValueType
			? Expression.Unbox(param, declaringType)
			: Expression.Convert(param, declaringType);
		Expression property = Expression.Property(instance, prop);
		Expression box = Expression.Convert(property, typeof(object));
		return Expression.Lambda<Func<object, object>>(box, param).Compile();
	}
}

internal static class ObjectSaveCache
{
	private static readonly ConcurrentDictionary<Type, ObjectPropertyAccessor[]> _cache = new();

	public static ObjectPropertyAccessor[] GetProperties(Type type)
	{
		return _cache.GetOrAdd(type, static t =>
		{
			var propInfos = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
			var list = new List<ObjectPropertyAccessor>(propInfos.Length);
			for (int i = 0; i < propInfos.Length; i++)
			{
				var p = propInfos[i];
				if (p.CanRead && p.GetIndexParameters().Length == 0)
					list.Add(new ObjectPropertyAccessor(p));
			}
			return list.ToArray();
		});
	}
}

internal static class ObjectSaveCache<T>
{
	public static readonly ObjectPropertyAccessor[] Properties = ObjectSaveCache.GetProperties(typeof(T));
}

internal static partial class SaveQuery
{
	internal static SaveParams Create<T>(T obj, string table, string idProp)
	{
		ArgumentNullException.ThrowIfNull(obj);
		var properties = obj.GetType() == typeof(T)
			? ObjectSaveCache<T>.Properties
			: ObjectSaveCache.GetProperties(obj.GetType());
		return Create(properties.Select(property => (property.Name, property.Getter(obj))),
			table, idProp, StringComparison.OrdinalIgnoreCase);
	}
}

public static partial class Db
{
	/// <summary>Inserts an object with a missing or zero ID, or updates a row by its ID.</summary>
	/// <returns>The inserted or updated ID, or zero if no row was updated.</returns>
	public static int Save<T>(T obj, string table, string idProp = "Id")
		=> Save(SaveQuery.Create(obj, table, idProp));
}
