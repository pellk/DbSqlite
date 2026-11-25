using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nuel.Sqlite;

namespace Db.Tests;

public enum TestStatus
{
	Inactive = 0,
	Active = 1,
	Suspended = 2
}

[TestClass]
public class DictionaryTests
{
	[TestMethod]
	public void Dictionary_NullQuery_ThrowsArgumentNullException()
	{
		Assert.ThrowsExactly<ArgumentNullException>(() =>
			nuel.Sqlite.Db.Dictionary<int, string>(null!));
	}

	[TestMethod]
	public void Dictionary_WhitespaceQuery_ThrowsArgumentException()
	{
		Assert.ThrowsExactly<ArgumentException>(() =>
			nuel.Sqlite.Db.Dictionary<int, string>("   "));
	}

	[TestMethod]
	public void ReadDictionary_EmptyTable_ReturnsNull()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string>(reader);

		Assert.IsNull(result);
	}

	[TestMethod]
	public void ReadDictionary_SingleColumn_ReturnsNull()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Rows.Add(1);

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string>(reader);

		Assert.IsNull(result);
	}

	[TestMethod]
	public void ReadDictionary_ValidRows_MapsCorrectly()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));
		dt.Rows.Add(1, "Alice");
		dt.Rows.Add(2, "Bob");

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual("Alice", result[1]);
		Assert.AreEqual("Bob", result[2]);
	}

	[TestMethod]
	public void ReadDictionary_NullValue_MapsToDefaultWithoutThrowing()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));
		dt.Rows.Add(1, DBNull.Value);
		dt.Rows.Add(2, "Bob");

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string?>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(2, result.Count);
		Assert.IsNull(result[1]);
		Assert.AreEqual("Bob", result[2]);
	}

	[TestMethod]
	public void ReadDictionary_NullKey_SkipsRowSafely()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));
		dt.Rows.Add(DBNull.Value, "NoKey");
		dt.Rows.Add(1, "HasKey");

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual("HasKey", result[1]);
	}

	[TestMethod]
	public void ReadDictionary_TypeConversion_ConvertsKeysAndValues()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int)); // Int32 in DB
		dt.Columns.Add("Amount", typeof(decimal)); // Decimal in DB
		dt.Rows.Add(100, 25.50m);

		using var reader = dt.CreateDataReader();
		// Mapping to long key and double value
		var result = nuel.Sqlite.Db.ReadDictionary<long, double>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(1, result.Count);
		Assert.IsTrue(result.ContainsKey(100L));
		Assert.AreEqual(25.50, result[100L], 0.001);
	}

	[TestMethod]
	public void ReadDictionary_EnumConversion_ConvertsEnumValue()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Status", typeof(int));
		dt.Rows.Add(1, 1); // Active
		dt.Rows.Add(2, 2); // Suspended

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, TestStatus>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(2, result.Count);
		Assert.AreEqual(TestStatus.Active, result[1]);
		Assert.AreEqual(TestStatus.Suspended, result[2]);
	}

	[TestMethod]
	public void ReadDictionary_GuidConversion_ConvertsStringValueToGuid()
	{
		var guid = Guid.NewGuid();
		using var dt = new DataTable();
		dt.Columns.Add("Name", typeof(string));
		dt.Columns.Add("GuidStr", typeof(string));
		dt.Rows.Add("Item1", guid.ToString());

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<string, Guid>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual(guid, result["Item1"]);
	}

	[TestMethod]
	public void ReadDictionary_DuplicateKeys_UpdatesLastValueWithoutThrowing()
	{
		using var dt = new DataTable();
		dt.Columns.Add("Id", typeof(int));
		dt.Columns.Add("Name", typeof(string));
		dt.Rows.Add(1, "First");
		dt.Rows.Add(1, "Second");

		using var reader = dt.CreateDataReader();
		var result = nuel.Sqlite.Db.ReadDictionary<int, string>(reader);

		Assert.IsNotNull(result);
		Assert.AreEqual(1, result.Count);
		Assert.AreEqual("Second", result[1]);
	}
}

